using System;
using System.IO;
using System.Text.Json;
using GuguSolucoes.Desktop.Core;

namespace GuguSolucoes.Desktop.Infrastructure;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly AppPaths _paths;
    private readonly AppLogger _logger;

    public SettingsStore(AppPaths paths, AppLogger logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_paths.SettingsFilePath))
        {
            var defaults = AppSettings.CreateDefault();
            Save(defaults);
            return defaults;
        }

        try
        {
            var raw = File.ReadAllText(_paths.SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(raw, JsonOptions);
            settings ??= AppSettings.CreateDefault();
            var normalized = NormalizeCurrentDefaults(settings);
            if (normalized)
            {
                Save(settings);
            }

            return settings;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Falha ao ler settings.json. Usando padrão. Detalhe: {ex.Message}");
            return AppSettings.CreateDefault();
        }
    }

    public void Save(AppSettings settings)
    {
        var raw = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_paths.SettingsFilePath, raw);
    }

    private static bool NormalizeCurrentDefaults(AppSettings settings)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(settings.GitHubRepo) ||
            string.Equals(settings.GitHubRepo, "gugu-solucoes/GuguSolucoes", StringComparison.OrdinalIgnoreCase))
        {
            settings.GitHubRepo = "luizgdsd/GuguSolucoes";
            changed = true;
        }

        if (!settings.EnableAutoUpdate)
        {
            settings.EnableAutoUpdate = true;
            changed = true;
        }

        if (settings.UpdateCheckIntervalMinutes < 1)
        {
            settings.UpdateCheckIntervalMinutes = 10;
            changed = true;
        }

        if (!settings.NotifyOnUpdate)
        {
            settings.NotifyOnUpdate = true;
            changed = true;
        }

        return changed;
    }
}


