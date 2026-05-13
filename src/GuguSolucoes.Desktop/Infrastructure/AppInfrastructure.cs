using System;
using System.Diagnostics;
using System.IO;

namespace GuguSolucoes.Desktop.Infrastructure;

public sealed class AppPaths
{
    public string BaseDirectory { get; }
    public string ConfigDirectory { get; }
    public string LogsDirectory { get; }
    public string SettingsFilePath { get; }
    public string LastUpdateFilePath { get; }

    public AppPaths()
    {
        BaseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GuguSolucoes");
        ConfigDirectory = Path.Combine(BaseDirectory, "config");
        LogsDirectory = Path.Combine(BaseDirectory, "logs");
        SettingsFilePath = Path.Combine(ConfigDirectory, "settings.json");
        LastUpdateFilePath = Path.Combine(ConfigDirectory, "last-update.json");

        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    public string CurrentLogFilePath => Path.Combine(LogsDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
}

public sealed class AppLogger
{
    private readonly object _lock = new();
    private readonly AppPaths _paths;

    public AppLogger(AppPaths paths)
    {
        _paths = paths;
    }

    public event Action<string>? LineLogged;

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}][{level}] {message}";

        lock (_lock)
        {
            File.AppendAllText(_paths.CurrentLogFilePath, line + Environment.NewLine);
        }

        Debug.WriteLine(line);
        LineLogged?.Invoke(line);
    }
}

