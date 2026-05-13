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
            return settings ?? AppSettings.CreateDefault();
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
}


