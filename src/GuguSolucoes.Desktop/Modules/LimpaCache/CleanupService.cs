using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GuguSolucoes.Desktop.Modules.LimpaCache;

public sealed class TempCleanupService
{
    private readonly CleanupLogWriter _log;

    public TempCleanupService(CleanupLogWriter log)
    {
        _log = log;
    }

    public CleanupResult Run(CleanupConfig? config)
    {
        var effectiveConfig = config ?? new CleanupConfig();
        var result = new CleanupResult
        {
            StartedAtUtc = DateTime.UtcNow
        };

        foreach (var target in ResolveTargets(effectiveConfig))
        {
            CleanTarget(target, result);
        }

        result.FinishedAtUtc = DateTime.UtcNow;
        result.DurationSeconds = (int)Math.Round((result.FinishedAtUtc - result.StartedAtUtc).TotalSeconds);

        _log.Info(
            $"Limpeza finalizada. Arquivos: {result.DeletedFiles}, Pastas: {result.DeletedDirectories}, " +
            $"Liberado: {result.FreedBytes} bytes, Falhas: {result.FailedItems}, Ignorados: {result.SkippedItems}.");

        return result;
    }

    private static IEnumerable<string> ResolveTargets(CleanupConfig config)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (config.CleanWindowsTemp)
        {
            var windowsTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
            if (!string.IsNullOrWhiteSpace(windowsTemp))
            {
                targets.Add(windowsTemp);
            }
        }

        if (config.CleanUsersTemp)
        {
            foreach (var userTemp in GetUsersTempDirectories())
            {
                targets.Add(userTemp);
            }
        }

        var runtimeTemp = Path.GetTempPath();
        if (!string.IsNullOrWhiteSpace(runtimeTemp))
        {
            targets.Add(runtimeTemp);
        }

        return targets;
    }

    private static IEnumerable<string> GetUsersTempDirectories()
    {
        var systemDrive = Environment.GetEnvironmentVariable("SystemDrive");
        if (string.IsNullOrWhiteSpace(systemDrive))
        {
            systemDrive = "C:";
        }

        var usersRoot = Path.Combine(systemDrive, "Users");
        if (!Directory.Exists(usersRoot))
        {
            yield break;
        }

        var ignoredProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "All Users",
            "Default",
            "Default User",
            "Public",
            "WDAGUtilityAccount",
            "defaultuser0"
        };

        string[] profiles;
        try
        {
            profiles = Directory.GetDirectories(usersRoot);
        }
        catch
        {
            yield break;
        }

        foreach (var profile in profiles)
        {
            var profileName = Path.GetFileName(profile);
            if (ignoredProfiles.Contains(profileName))
            {
                continue;
            }

            var tempPath = Path.Combine(profile, "AppData", "Local", "Temp");
            if (Directory.Exists(tempPath))
            {
                yield return tempPath;
            }
        }
    }

    private void CleanTarget(string target, CleanupResult result)
    {
        if (!Directory.Exists(target))
        {
            return;
        }

        result.CleanedTargets.Add(target);
        _log.Info("Limpando: " + target);

        foreach (var filePath in SafeGetFiles(target))
        {
            DeleteFile(filePath, result);
        }

        foreach (var directoryPath in SafeGetDirectories(target))
        {
            DeleteDirectoryRecursively(directoryPath, result);
        }
    }

    private static string[] SafeGetFiles(string path)
    {
        try
        {
            return Directory.GetFiles(path);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string[] SafeGetDirectories(string path)
    {
        try
        {
            return Directory.GetDirectories(path);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private void DeleteDirectoryRecursively(string directoryPath, CleanupResult result)
    {
        if (IsReparsePoint(directoryPath))
        {
            result.SkippedItems++;
            return;
        }

        foreach (var filePath in SafeGetFiles(directoryPath))
        {
            DeleteFile(filePath, result);
        }

        foreach (var childDirectory in SafeGetDirectories(directoryPath))
        {
            DeleteDirectoryRecursively(childDirectory, result);
        }

        TryDeleteDirectoryIfEmpty(directoryPath, result);
    }

    private static bool IsReparsePoint(string directoryPath)
    {
        try
        {
            var attributes = File.GetAttributes(directoryPath);
            return (attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
        catch
        {
            return true;
        }
    }

    private void DeleteFile(string filePath, CleanupResult result)
    {
        long fileLength = 0;

        try
        {
            var info = new FileInfo(filePath);
            if (info.Exists)
            {
                fileLength = info.Length;
            }

            File.SetAttributes(filePath, FileAttributes.Normal);
            File.Delete(filePath);

            result.DeletedFiles++;
            result.FreedBytes += fileLength;
        }
        catch (IOException)
        {
            result.SkippedItems++;
        }
        catch (UnauthorizedAccessException ex)
        {
            result.FailedItems++;
            RegisterError(result, $"Sem permissão para remover: {filePath} ({ex.Message})");
        }
        catch (Exception ex)
        {
            result.FailedItems++;
            RegisterError(result, $"Falha ao remover arquivo: {filePath} ({ex.Message})");
        }
    }

    private void TryDeleteDirectoryIfEmpty(string directoryPath, CleanupResult result)
    {
        try
        {
            if (Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                return;
            }

            File.SetAttributes(directoryPath, FileAttributes.Normal);
            Directory.Delete(directoryPath, recursive: false);
            result.DeletedDirectories++;
        }
        catch (IOException)
        {
            result.SkippedItems++;
        }
        catch (UnauthorizedAccessException ex)
        {
            result.FailedItems++;
            RegisterError(result, $"Sem permissão para remover pasta: {directoryPath} ({ex.Message})");
        }
        catch (Exception ex)
        {
            result.FailedItems++;
            RegisterError(result, $"Falha ao remover pasta: {directoryPath} ({ex.Message})");
        }
    }

    private void RegisterError(CleanupResult result, string message)
    {
        if (result.Errors.Count < 20)
        {
            result.Errors.Add(message);
        }

        _log.Error(message);
    }
}

