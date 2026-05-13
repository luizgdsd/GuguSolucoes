using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using GuguSolucoes.Desktop.Infrastructure;

namespace GuguSolucoes.Desktop.Core;

public sealed class RepairService
{
    private readonly AppLogger _logger;

    public RepairService(AppLogger logger)
    {
        _logger = logger;
    }

    public async Task<RepairResult> RunAsync(
        AppSettings settings,
        IProgress<RepairProgress> progress,
        CancellationToken cancellationToken,
        bool forceFullRepair = false)
    {
        var domains = settings.Domains?.Where(static d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            ?? new List<string>();

        if (domains.Count == 0)
        {
            domains.Add("www.gov.br");
            domains.Add("acesso.gov.br");
        }

        Report(progress, 5, "Inicialização", "Iniciando diagnóstico de conectividade.");

        var before = await ValidateDomainsAsync(domains, progress, 10, 35, cancellationToken).ConfigureAwait(false);
        var hasFailure = before.Any(static r => !(r.DnsOk && r.Tcp443Ok));
        var shouldRepair = hasFailure || forceFullRepair;

        if (!shouldRepair)
        {
            _logger.Info("Nenhuma falha detectada. Reparo não necessário.");
            Report(progress, 100, "Concluído", "Nenhuma falha detectada.");
            return new RepairResult
            {
                Success = true,
                RepairPerformed = false,
                ExitCode = 0,
                Summary = "Nenhuma falha detectada."
            };
        }

        if (!hasFailure && forceFullRepair)
        {
            _logger.Info("Nenhuma falha detectada, mas o reparo completo foi solicitado manualmente.");
            Report(progress, 38, "Reparo", "Sem falha detectada. Aplicando manutenção preventiva.");
        }

        Report(progress, 40, "Reparo", "Aplicando reparos locais.");
        CloseBrowsers(progress, cancellationToken);
        RefreshNetworkStack(progress, cancellationToken);

        if (settings.ClearBrowserCaches)
        {
            ClearBrowserCaches(progress, cancellationToken);
        }
        else
        {
            _logger.Info("Limpeza de cache desativada na configuração.");
            Report(progress, 75, "Cache", "Limpeza de cache desativada.");
        }

        Report(progress, 85, "Validação", "Revalidando conectividade após reparo.");
        var after = await ValidateDomainsAsync(domains, progress, 86, 99, cancellationToken).ConfigureAwait(false);
        var success = after.All(static r => r.DnsOk && r.Tcp443Ok);

        if (success)
        {
            _logger.Info("Conectividade validada com sucesso após reparo.");
            Report(progress, 100, "Concluído", "Conectividade restaurada.");
            return new RepairResult
            {
                Success = true,
                RepairPerformed = true,
                ExitCode = 0,
                Summary = "Reparo concluído com sucesso."
            };
        }

        _logger.Warn("Ainda há falhas após o reparo. Verifique proxy/firewall/restrições externas.");
        Report(progress, 100, "Concluído", "Reparo finalizado com alertas.");
        return new RepairResult
        {
            Success = false,
            RepairPerformed = true,
            ExitCode = 2,
            Summary = "Persistem falhas de conectividade após reparo."
        };
    }

    private async Task<List<ConnectivityResult>> ValidateDomainsAsync(
        IReadOnlyList<string> domains,
        IProgress<RepairProgress> progress,
        int startPercent,
        int endPercent,
        CancellationToken cancellationToken)
    {
        var results = new List<ConnectivityResult>(domains.Count);
        var span = Math.Max(1, endPercent - startPercent);

        for (var i = 0; i < domains.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var domain = domains[i].Trim();
            var percent = startPercent + (int)((i / (double)Math.Max(1, domains.Count)) * span);
            Report(progress, percent, "Diagnóstico", $"Testando {domain}...");

            var result = await TestEndpointAsync(domain, cancellationToken).ConfigureAwait(false);
            results.Add(result);

            if (result.DnsOk && result.Tcp443Ok)
            {
                _logger.Info($"OK: {domain} (DNS e TCP/443). Detalhe: {result.Details}");
            }
            else
            {
                _logger.Warn($"Falha: {domain}. DNS={result.DnsOk} TCP443={result.Tcp443Ok}. Detalhe: {result.Details}");
            }
        }

        return results;
    }

    private static async Task<ConnectivityResult> TestEndpointAsync(string domain, CancellationToken cancellationToken)
    {
        bool dnsOk;
        string details;

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(domain, cancellationToken).ConfigureAwait(false);
            dnsOk = addresses.Length > 0;
            details = dnsOk ? string.Join(", ", addresses.Select(static a => a.ToString())) : "Sem endereço DNS";
        }
        catch (Exception ex)
        {
            return new ConnectivityResult
            {
                Domain = domain,
                DnsOk = false,
                Tcp443Ok = false,
                Details = ex.Message
            };
        }

        var tcpOk = false;
        if (dnsOk)
        {
            tcpOk = await TestTcpAsync(domain, 443, TimeSpan.FromSeconds(6), cancellationToken).ConfigureAwait(false);
        }

        return new ConnectivityResult
        {
            Domain = domain,
            DnsOk = dnsOk,
            Tcp443Ok = tcpOk,
            Details = details
        };
    }

    private static async Task<bool> TestTcpAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await client.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void CloseBrowsers(IProgress<RepairProgress> progress, CancellationToken cancellationToken)
    {
        Report(progress, 48, "Reparo", "Fechando navegadores ativos.");

        foreach (var name in new[] { "chrome", "msedge", "firefox", "vivaldi" })
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var process in Process.GetProcessesByName(name))
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.CloseMainWindow();
                        if (!process.WaitForExit(1200))
                        {
                            process.Kill(true);
                        }
                        _logger.Info($"Processo encerrado: {process.ProcessName} (PID {process.Id}).");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Não foi possível encerrar {process.ProcessName} (PID {process.Id}): {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
    }

    private void RefreshNetworkStack(IProgress<RepairProgress> progress, CancellationToken cancellationToken)
    {
        RunIpConfigCommand(progress, 56, "Rede", "/flushdns", "Limpando cache DNS.", 12000, cancellationToken);
        RunIpConfigCommand(progress, 62, "Rede", "/release", "Liberando endereços IP (release).", 30000, cancellationToken);
        RunIpConfigCommand(progress, 70, "Rede", "/renew", "Renovando endereços IP (renew).", 45000, cancellationToken);
    }

    private void RunIpConfigCommand(
        IProgress<RepairProgress> progress,
        int percent,
        string stage,
        string arguments,
        string message,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        Report(progress, percent, stage, message);
        cancellationToken.ThrowIfCancellationRequested();

        var commandLabel = $"ipconfig {arguments}";
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                _logger.Warn($"Não foi possível iniciar {commandLabel}.");
                return;
            }

            if (!process.WaitForExit(timeoutMs))
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                    // Ignora erro ao tentar finalizar processo travado.
                }

                _logger.Warn($"Timeout executando {commandLabel}.");
                return;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            var error = process.StandardError.ReadToEnd().Trim();
            if (process.ExitCode == 0)
            {
                _logger.Info($"{commandLabel} executado com sucesso.");
            }
            else
            {
                var detail = !string.IsNullOrWhiteSpace(error) ? error : output;
                _logger.Warn($"Falha em {commandLabel} (exit {process.ExitCode}). {detail}");
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Falha ao executar {commandLabel}: {ex.Message}");
        }
    }

    private void ClearBrowserCaches(IProgress<RepairProgress> progress, CancellationToken cancellationToken)
    {
        var targets = GetBrowserCacheTargets();
        if (targets.Count == 0)
        {
            _logger.Info("Nenhuma pasta de cache encontrada para limpeza.");
            Report(progress, 75, "Cache", "Nenhuma pasta de cache encontrada.");
            return;
        }

        var allowedRoots = GetAllowedRoots();
        Report(progress, 76, "Cache", "Limpando caches dos navegadores.");

        for (var i = 0; i < targets.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var target = targets[i];
            var percent = 77 + (int)((i / (double)Math.Max(1, targets.Count)) * 6);
            Report(progress, percent, "Cache", $"Limpando {target}");

            if (!IsPathSafe(target, allowedRoots))
            {
                _logger.Warn($"Caminho ignorado por segurança: {target}");
                continue;
            }

            TryClearDirectoryContents(target);
        }

        _logger.Info("Limpeza de cache finalizada.");
    }

    private static List<string> GetBrowserCacheTargets()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddChromiumTargets(result, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data"));
        AddChromiumTargets(result, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "User Data"));
        AddChromiumTargets(result, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vivaldi", "User Data"));

        var firefoxLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mozilla", "Firefox", "Profiles");
        var firefoxRoaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla", "Firefox", "Profiles");
        AddFirefoxTargets(result, firefoxLocal);
        AddFirefoxTargets(result, firefoxRoaming);

        return result.Where(Directory.Exists).OrderBy(static p => p, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddChromiumTargets(ISet<string> targetSet, string userDataRoot)
    {
        if (!Directory.Exists(userDataRoot))
        {
            return;
        }

        foreach (var profile in Directory.GetDirectories(userDataRoot))
        {
            var name = Path.GetFileName(profile);
            if (!string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase) &&
                !name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            targetSet.Add(Path.Combine(profile, "Cache"));
            targetSet.Add(Path.Combine(profile, "Code Cache"));
            targetSet.Add(Path.Combine(profile, "GPUCache"));
            targetSet.Add(Path.Combine(profile, "Media Cache"));
            targetSet.Add(Path.Combine(profile, "Network", "Cache"));
            targetSet.Add(Path.Combine(profile, "Service Worker", "CacheStorage"));
        }
    }

    private static void AddFirefoxTargets(ISet<string> targetSet, string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var profile in Directory.GetDirectories(root))
        {
            targetSet.Add(Path.Combine(profile, "cache2"));
            targetSet.Add(Path.Combine(profile, "startupCache"));
        }
    }

    private static IReadOnlyList<string> GetAllowedRoots()
    {
        return new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "User Data"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vivaldi", "User Data"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mozilla", "Firefox", "Profiles"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla", "Firefox", "Profiles")
        };
    }

    private static bool IsPathSafe(string path, IReadOnlyList<string> allowedRoots)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var root in allowedRoots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void TryClearDirectoryContents(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            try
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    Directory.Delete(entry, true);
                }
                else
                {
                    File.SetAttributes(entry, FileAttributes.Normal);
                    File.Delete(entry);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Falha ao limpar item de cache '{entry}': {ex.Message}");
            }
        }

        _logger.Info($"Cache limpo: {directory}");
    }

    private static void Report(IProgress<RepairProgress> progress, int percent, string stage, string message)
    {
        progress.Report(new RepairProgress(Math.Clamp(percent, 0, 100), stage, message));
    }
}


