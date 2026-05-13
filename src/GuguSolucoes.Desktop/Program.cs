using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using GuguSolucoes.Desktop.Modules.LimpaCache;

namespace GuguSolucoes.Desktop;

internal static class Program
{
    private const string UiSingleInstanceMutexName = @"Local\GuguSolucoes.Desktop.SingleInstance";

    [STAThread]
    private static int Main(string[] args)
    {
        if (IsAgentMode(args))
        {
            return new CleanupAgentRunner().Run(args);
        }

        using var singleInstanceMutex = new Mutex(initiallyOwned: true, UiSingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            return 0;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            var startInTray = HasArg(args, "--tray") || HasArg(args, "--minimized") || HasArg(args, "--background");
            Application.Run(new MainForm(startInTray));
            return 0;
        }
        finally
        {
            try
            {
                singleInstanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex ownership can already be lost during shutdown; safe to ignore.
            }
        }
    }

    private static bool IsAgentMode(string[] args)
    {
        return HasArg(args, "--agent") || HasArg(args, "--limpacache-agent");
    }

    private static bool HasArg(string[] args, string value)
    {
        return args.Any(arg => string.Equals(arg, value, StringComparison.OrdinalIgnoreCase));
    }
}

