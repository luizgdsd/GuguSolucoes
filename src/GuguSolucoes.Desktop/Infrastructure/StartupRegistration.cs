using System;
using System.IO;

namespace GuguSolucoes.Desktop.Infrastructure;

public sealed class StartupRegistration
{
    private const string ShortcutName = "GuguSolucoes.lnk";

    public void Ensure(bool enabled)
    {
        var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        var shortcutPath = Path.Combine(startupFolder, ShortcutName);

        if (!enabled)
        {
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }
            return;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return;
        }

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            return;
        }

        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = exePath;
        shortcut.Arguments = "--tray";
        shortcut.WorkingDirectory = Path.GetDirectoryName(exePath) ?? startupFolder;
        shortcut.Description = "Gugu Soluções";
        shortcut.Save();
    }
}

