using System;

namespace GuguSolucoes.Desktop.Core;

public sealed record RepairProgress(int Percent, string Stage, string Message, bool IsIndeterminate = false);

public sealed class RepairResult
{
    public bool Success { get; init; }
    public bool RepairPerformed { get; init; }
    public int ExitCode { get; init; }
    public string Summary { get; init; } = string.Empty;
}

public sealed class ConnectivityResult
{
    public string Domain { get; init; } = string.Empty;
    public bool DnsOk { get; init; }
    public bool Tcp443Ok { get; init; }
    public string Details { get; init; } = string.Empty;
}

public sealed class UpdateCheckResult
{
    public bool Success { get; init; }
    public bool UpdateAvailable { get; init; }
    public Version? LatestVersion { get; init; }
    public string LatestTag { get; init; } = string.Empty;
    public string ReleaseName { get; init; } = string.Empty;
    public string InstallerUrl { get; init; } = string.Empty;
    public string InstallerName { get; init; } = string.Empty;
    public string Sha256 { get; init; } = string.Empty;
    public bool Mandatory { get; init; }
    public string ReleaseNotes { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class UpdateApplyResult
{
    public bool StartedInstaller { get; init; }
    public string DownloadedFilePath { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

