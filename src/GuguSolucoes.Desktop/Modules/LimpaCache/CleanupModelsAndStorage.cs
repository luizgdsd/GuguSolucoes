using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace GuguSolucoes.Desktop.Modules.LimpaCache;

public sealed class CleanupConfig
{
    public bool AutoCleanupEnabled { get; set; } = false;
    public int IntervalMinutes { get; set; } = 60;
    public bool CleanWindowsTemp { get; set; } = true;
    public bool CleanUsersTemp { get; set; } = true;

    public int GetValidatedIntervalMinutes()
    {
        return Math.Clamp(IntervalMinutes, 5, 1440);
    }
}

public sealed class CleanupResult
{
    public DateTime StartedAtUtc { get; set; }
    public DateTime FinishedAtUtc { get; set; }
    public int DurationSeconds { get; set; }
    public int DeletedFiles { get; set; }
    public int DeletedDirectories { get; set; }
    public long FreedBytes { get; set; }
    public int SkippedItems { get; set; }
    public int FailedItems { get; set; }
    public List<string> CleanedTargets { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public sealed class CleanupState
{
    public int TotalRuns { get; set; }
    public DateTime? LastSuccessfulRunUtc { get; set; }
    public CleanupResult? LastRun { get; set; }
}

public static class CleanupPaths
{
    private const string ProductFolder = "LimpaCache";

    public static string ProgramDataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), ProductFolder);

    public static string LogDirectory => Path.Combine(ProgramDataRoot, "logs");
    public static string ConfigFilePath => Path.Combine(ProgramDataRoot, "config.json");
    public static string StateFilePath => Path.Combine(ProgramDataRoot, "state.json");
    public static string AgentTaskName => "GuguSolucoes TempCleanup Agent";

    public static void EnsureStructure()
    {
        Directory.CreateDirectory(ProgramDataRoot);
        Directory.CreateDirectory(LogDirectory);
    }
}

public sealed class CleanupLogWriter
{
    private readonly object _sync = new();

    public void Info(string message) => Write("INFO", message);
    public void Warning(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        try
        {
            CleanupPaths.EnsureStructure();
            var fileName = $"{DateTime.Now:yyyy-MM-dd}.log";
            var fullPath = Path.Combine(CleanupPaths.LogDirectory, fileName);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";

            lock (_sync)
            {
                File.AppendAllText(fullPath, line + Environment.NewLine, new UTF8Encoding(false));
            }
        }
        catch
        {
            // Não interromper fluxo principal por falha de log.
        }
    }
}

public sealed class CleanupConfigStore
{
    private readonly JsonFileStore<CleanupConfig> _store;
    private readonly CleanupLogWriter _log;

    public CleanupConfigStore(string filePath, CleanupLogWriter log)
    {
        _store = new JsonFileStore<CleanupConfig>(filePath);
        _log = log;
    }

    public CleanupConfig Load()
    {
        var config = _store.LoadOrDefault(static () => new CleanupConfig());
        config.IntervalMinutes = config.GetValidatedIntervalMinutes();
        return config;
    }

    public void Save(CleanupConfig config)
    {
        config.IntervalMinutes = config.GetValidatedIntervalMinutes();
        _store.Save(config);
        _log.Info("Configuração salva com sucesso.");
    }
}

public sealed class CleanupStateStore
{
    private readonly JsonFileStore<CleanupState> _store;
    private readonly CleanupLogWriter _log;

    public CleanupStateStore(string filePath, CleanupLogWriter log)
    {
        _store = new JsonFileStore<CleanupState>(filePath);
        _log = log;
    }

    public CleanupState Load()
    {
        return _store.LoadOrDefault(static () => new CleanupState());
    }

    public void SaveRun(CleanupResult result)
    {
        var state = Load();
        state.TotalRuns++;
        state.LastRun = result;

        if (result.FailedItems == 0)
        {
            state.LastSuccessfulRunUtc = result.FinishedAtUtc;
        }

        _store.Save(state);
        _log.Info("Estado atualizado após execução de limpeza.");
    }
}

public static class CleanupSecurityContext
{
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static string CurrentIdentity()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.Name ?? "desconhecido";
    }
}

public static class CleanupSizeFormatter
{
    public static string Humanize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        double value = bytes;
        var units = new[] { "KB", "MB", "GB", "TB" };
        var unitIndex = -1;

        do
        {
            value /= 1024d;
            unitIndex++;
        }
        while (value >= 1024d && unitIndex < units.Length - 1);

        return $"{value:0.##} {units[unitIndex]}";
    }
}

internal sealed class JsonFileStore<T> where T : class
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly object _sync = new();
    private readonly string _filePath;

    public JsonFileStore(string filePath)
    {
        _filePath = filePath;
    }

    public T LoadOrDefault(Func<T> defaultFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultFactory);

        lock (_sync)
        {
            if (!File.Exists(_filePath))
            {
                return defaultFactory();
            }

            try
            {
                var raw = File.ReadAllText(_filePath, Encoding.UTF8);
                var value = JsonSerializer.Deserialize<T>(raw, JsonOptions);
                return value ?? defaultFactory();
            }
            catch
            {
                return defaultFactory();
            }
        }
    }

    public void Save(T data)
    {
        ArgumentNullException.ThrowIfNull(data);

        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _filePath + ".tmp";
            var raw = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(tempPath, raw, new UTF8Encoding(false));

            if (File.Exists(_filePath))
            {
                File.Replace(tempPath, _filePath, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, _filePath);
            }
        }
    }
}


