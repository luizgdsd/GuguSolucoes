using System;
using System.Linq;
using System.Threading;

namespace GuguSolucoes.Desktop.Modules.LimpaCache;

public sealed class CleanupAgentRunner
{
    private static readonly ManualResetEvent ShutdownEvent = new(initialState: false);
    private const string AgentMutexName = @"Global\GuguSolucoes.CleanupAgent.Singleton";

    public int Run(string[] args)
    {
        CleanupPaths.EnsureStructure();

        var log = new CleanupLogWriter();
        var runOnce = HasArg(args, "--once");

        using var mutex = new Mutex(initiallyOwned: false, AgentMutexName, out var createdNew);
        if (!createdNew)
        {
            log.Warning("Uma instância do agente de limpeza já está em execução.");
            return 0;
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) => ShutdownEvent.Set();

        var configStore = new CleanupConfigStore(CleanupPaths.ConfigFilePath, log);
        var stateStore = new CleanupStateStore(CleanupPaths.StateFilePath, log);
        var cleaner = new TempCleanupService(log);

        log.Info("Agente LimpaCache iniciado por: " + CleanupSecurityContext.CurrentIdentity());
        log.Info("Execução elevada: " + CleanupSecurityContext.IsElevated());

        if (runOnce)
        {
            ExecuteCycle(cleaner, configStore, stateStore, log, forceExecution: true);
            log.Info("Execução única concluída.");
            return 0;
        }

        var currentInterval = ExecuteCycle(cleaner, configStore, stateStore, log, forceExecution: false);

        while (true)
        {
            log.Info($"Aguardando {currentInterval} minuto(s) ate a proxima verificacao.");

            if (ShutdownEvent.WaitOne(TimeSpan.FromMinutes(currentInterval)))
            {
                break;
            }

            currentInterval = ExecuteCycle(cleaner, configStore, stateStore, log, forceExecution: false);
        }

        log.Info("Agente LimpaCache finalizado.");
        return 0;
    }

    private static bool HasArg(string[] args, string value)
    {
        return args.Any(arg => string.Equals(arg, value, StringComparison.OrdinalIgnoreCase));
    }

    private static int ExecuteCycle(
        TempCleanupService cleaner,
        CleanupConfigStore configStore,
        CleanupStateStore stateStore,
        CleanupLogWriter log,
        bool forceExecution)
    {
        try
        {
            var config = configStore.Load();
            var interval = config.GetValidatedIntervalMinutes();

            if (!forceExecution && !config.AutoCleanupEnabled)
            {
                log.Info("Limpeza automática desativada na configuração. Ciclo ignorado.");
                return interval;
            }

            var result = cleaner.Run(config);
            stateStore.SaveRun(result);
            return interval;
        }
        catch (Exception ex)
        {
            log.Error("Falha no ciclo de limpeza: " + ex);
            return 30;
        }
    }
}


