using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using GuguSolucoes.Desktop.Infrastructure;

namespace GuguSolucoes.Desktop.Core;

public sealed class UpdateService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppPaths _paths;
    private readonly AppLogger _logger;
    private readonly HttpClient _httpClient;

    public UpdateService(AppPaths paths, AppLogger logger)
    {
        _paths = paths;
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GuguSolucoes-Updater/1.0");
    }

    public Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    public async Task<UpdateCheckResult> CheckForUpdateAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.EnableAutoUpdate)
        {
            return new UpdateCheckResult
            {
                Success = true,
                UpdateAvailable = false,
                Message = "Auto update desativado."
            };
        }

        var repo = NormalizeRepository(settings.GitHubRepo);
        if (string.IsNullOrWhiteSpace(repo))
        {
            return new UpdateCheckResult
            {
                Success = false,
                Message = "Repositorio GitHub nao configurado."
            };
        }

        var release = await GetLatestReleaseAsync(repo, settings.GitHubToken, cancellationToken).ConfigureAwait(false);
        var manifest = await TryLoadManifestAsync(release, settings.GitHubToken, cancellationToken).ConfigureAwait(false);

        var latestVersionText = manifest?.Version ?? release.TagName;
        var latestVersion = ParseVersion(latestVersionText);
        if (latestVersion is null)
        {
            return new UpdateCheckResult
            {
                Success = false,
                LatestTag = release.TagName,
                Message = $"Nao foi possivel identificar a versao da release {release.TagName}."
            };
        }

        var installerUrl = string.IsNullOrWhiteSpace(manifest?.InstallerUrl)
            ? FindInstallerUrl(release)
            : manifest!.InstallerUrl;
        var installerName = Path.GetFileName(new Uri(installerUrl).LocalPath);

        var updateAvailable = latestVersion.CompareTo(NormalizeVersion(CurrentVersion)) > 0;

        return new UpdateCheckResult
        {
            Success = true,
            UpdateAvailable = updateAvailable,
            LatestVersion = latestVersion,
            LatestTag = release.TagName,
            ReleaseName = release.Name ?? release.TagName,
            InstallerUrl = installerUrl,
            InstallerName = string.IsNullOrWhiteSpace(installerName) ? $"GuguSolucoes-Setup-{latestVersion}.exe" : installerName,
            Sha256 = manifest?.Sha256 ?? string.Empty,
            Mandatory = manifest?.Mandatory ?? false,
            ReleaseNotes = manifest?.ReleaseNotes ?? release.Body ?? string.Empty,
            Message = updateAvailable
                ? $"Versao {latestVersion} disponivel."
                : $"Versao atual ({FormatVersion(CurrentVersion)}) ja esta instalada."
        };
    }

    public async Task<UpdateApplyResult> DownloadAndStartInstallerAsync(
        UpdateCheckResult update,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(update.InstallerUrl))
        {
            throw new InvalidOperationException("Release sem instalador disponivel.");
        }

        var versionFolder = update.LatestVersion?.ToString() ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var updateDirectory = Path.Combine(_paths.BaseDirectory, "updates", versionFolder);
        Directory.CreateDirectory(updateDirectory);

        var installerName = string.IsNullOrWhiteSpace(update.InstallerName)
            ? $"GuguSolucoes-Setup-{versionFolder}.exe"
            : update.InstallerName;
        var installerPath = Path.Combine(updateDirectory, installerName);

        await DownloadFileAsync(update.InstallerUrl, installerPath, settings.GitHubToken, cancellationToken).ConfigureAwait(false);
        ValidateSha256(installerPath, update.Sha256);

        var startInfo = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
            UseShellExecute = true,
            Verb = "runas"
        };

        Process.Start(startInfo);
        _logger.Info($"Instalador de update iniciado: {installerPath}");

        return new UpdateApplyResult
        {
            StartedInstaller = true,
            DownloadedFilePath = installerPath,
            Message = "Instalador de update iniciado."
        };
    }

    public static string FormatVersion(Version version)
    {
        var normalized = NormalizeVersion(version);
        return normalized.ToString();
    }

    private async Task<GitHubReleaseDto> GetLatestReleaseAsync(string repo, string token, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"https://api.github.com/repos/{repo}/releases/latest", token);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Falha ao consultar release ({(int)response.StatusCode}): {body}");
        }

        return JsonSerializer.Deserialize<GitHubReleaseDto>(body, JsonOptions)
            ?? throw new InvalidOperationException("Resposta de release invalida.");
    }

    private async Task<UpdateManifestDto?> TryLoadManifestAsync(
        GitHubReleaseDto release,
        string token,
        CancellationToken cancellationToken)
    {
        var manifestAsset = release.Assets.FirstOrDefault(static asset =>
            string.Equals(asset.Name, "update.json", StringComparison.OrdinalIgnoreCase));
        if (manifestAsset is null || string.IsNullOrWhiteSpace(manifestAsset.DownloadUrl))
        {
            return null;
        }

        try
        {
            using var request = CreateRequest(HttpMethod.Get, manifestAsset.DownloadUrl, token);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<UpdateManifestDto>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Nao foi possivel ler update.json da release: {ex.Message}");
            return null;
        }
    }

    private async Task DownloadFileAsync(string url, string targetPath, string token, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, url, token);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = File.Create(targetPath);
        await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        }

        return request;
    }

    private static string NormalizeRepository(string value)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["https://github.com/".Length..];
        }

        return normalized.Trim().TrimEnd('/');
    }

    private static string FindInstallerUrl(GitHubReleaseDto release)
    {
        var installer = release.Assets.FirstOrDefault(static asset =>
            asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
            asset.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase));

        if (installer is null || string.IsNullOrWhiteSpace(installer.DownloadUrl))
        {
            throw new InvalidOperationException("A ultima release nao possui instalador .exe anexado.");
        }

        return installer.DownloadUrl;
    }

    private static Version? ParseVersion(string value)
    {
        var normalized = value.Trim().TrimStart('v', 'V');
        return Version.TryParse(normalized, out var version) ? NormalizeVersion(version) : null;
    }

    private static Version NormalizeVersion(Version version)
    {
        return new Version(
            Math.Max(0, version.Major),
            Math.Max(0, version.Minor),
            Math.Max(0, version.Build));
    }

    private static void ValidateSha256(string filePath, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            return;
        }

        using var stream = File.OpenRead(filePath);
        var bytes = SHA256.HashData(stream);
        var actual = Convert.ToHexString(bytes);
        if (!string.Equals(actual, expectedHash.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(filePath);
            throw new InvalidOperationException("Hash SHA-256 do instalador nao confere. Download descartado.");
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("body")]
        public string? Body { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubAssetDto> Assets { get; init; } = new();
    }

    private sealed class GitHubAssetDto
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string DownloadUrl { get; init; } = string.Empty;
    }

    private sealed class UpdateManifestDto
    {
        public string Version { get; init; } = string.Empty;
        public string InstallerUrl { get; init; } = string.Empty;
        public string Sha256 { get; init; } = string.Empty;
        public bool Mandatory { get; init; }
        public string ReleaseNotes { get; init; } = string.Empty;
    }
}
