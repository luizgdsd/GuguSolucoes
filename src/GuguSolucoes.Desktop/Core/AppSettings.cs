using System.Collections.Generic;

namespace GuguSolucoes.Desktop.Core;

public sealed class AppSettings
{
    public string GitHubRepo { get; set; } = "luizgdsd/GuguSolucoes";
    public string GitHubToken { get; set; } = string.Empty;
    public bool EnableAutoUpdate { get; set; } = true;
    public int UpdateCheckIntervalMinutes { get; set; } = 10;
    public bool AutoApplyUpdates { get; set; } = false;
    public bool NotifyOnUpdate { get; set; } = true;
    public bool LaunchAtStartup { get; set; } = true;
    public bool ClearBrowserCaches { get; set; } = true;
    public List<string> Domains { get; set; } = new()
    {
        "www.gov.br",
        "acesso.gov.br"
    };

    public static AppSettings CreateDefault() => new();
}

