// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Microsoft.DotNet.Cli;

internal static class LoggerUtility
{
    public static FacadeLogger? DetermineBinlogger(string[] restoreArgs, string verb)
    {
        List<BinaryLogger> binaryLoggers = [];

        for (int i = restoreArgs.Length - 1; i >= 0; i--)
        {
            string blArg = restoreArgs[i];
            if (!IsBinLogArgument(blArg))
            {
                continue;
            }

            if (blArg.Contains(':'))
            {
                // split and forward args
                var split = blArg.Split(':', 2);
                var filename = split[1];
                if (filename.EndsWith(".binlog"))
                {
                    filename = filename.Substring(0, filename.Length - ".binlog".Length);
                    filename = $"{filename}-{verb}.binlog";
                }
                binaryLoggers.Add(new BinaryLogger { Parameters = filename });
            }
            else
            {
                // the same name will be used for the build and run-restore-exec steps, so we need to make sure they don't conflict
                var filename = $"msbuild-{verb}.binlog";
                binaryLoggers.Add(new BinaryLogger { Parameters = filename });
            }

            // Like in MSBuild, only the last binary logger is used.
            break;
        }

        // this binaryLogger needs to be used for both evaluation and execution, so we need to only call it with a single IEventSource across
        // both of those phases.
        // We need a custom logger to handle this, because the MSBuild API for evaluation and execution calls logger Initialize and Shutdown methods, so will not allow us to do this.
        if (binaryLoggers.Count > 0)
        {
            var fakeLogger = CreateFacadeLogger(binaryLoggers);

            return fakeLogger;
        }
        return null;
    }

    public static FacadeLogger CreateFacadeLogger(List<BinaryLogger> binaryLoggers)
    {
        var dispatcher = new PersistentDispatcher(binaryLoggers);
        return new FacadeLogger(dispatcher);
    }

    internal static void SeparateLoggerArguments(IEnumerable<string>? args, out ImmutableArray<string> loggerArgs, out ImmutableArray<string> nonLoggerArgs)
    {
        var loggerArgsBuilder = ImmutableArray.CreateBuilder<string>();
        var nonLoggerArgsBuilder = ImmutableArray.CreateBuilder<string>();

        foreach (var arg in args ?? [])
        {
            if (TryGetLoggerArgument(arg, out string? loggerArg))
            {
                loggerArgsBuilder.Add(loggerArg);
            }
            else
            {
                nonLoggerArgsBuilder.Add(arg);
            }
        }

        loggerArgs = loggerArgsBuilder.ToImmutable();
        nonLoggerArgs = nonLoggerArgsBuilder.ToImmutable();
    }

    internal static bool IsBinLogArgument(string arg)
    {
        const StringComparison comp = StringComparison.OrdinalIgnoreCase;
        return arg.StartsWith("/bl:", comp) || arg.Equals("/bl", comp)
            || arg.StartsWith("--binaryLogger:", comp) || arg.Equals("--binaryLogger", comp)
            || arg.StartsWith("-bl:", comp) || arg.Equals("-bl", comp);
    }

    internal static bool HasNoConsoleLoggerArgument(IEnumerable<string>? args) =>
        args?.Any(IsNoConsoleLoggerArgument) == true;

    private static bool IsNoConsoleLoggerArgument(string arg)
    {
        return TryParseSwitch(arg, out string? prefix, out string? switchName, out string? switchValue, out bool hasValue) &&
            prefix is "-" or "/" &&
            !hasValue &&
            switchName.Equals("noConsoleLogger", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetLoggerArgument(string arg, [NotNullWhen(true)] out string? loggerArg)
    {
        loggerArg = arg;
        if (IsBinLogArgument(arg) || IsNoConsoleLoggerArgument(arg))
        {
            return true;
        }

        if (!TryParseSwitch(arg, out string? prefix, out string? switchName, out string? switchValue, out bool hasValue))
        {
            loggerArg = null;
            return false;
        }

        const StringComparison comp = StringComparison.OrdinalIgnoreCase;
        if (switchName.Equals("tl", comp) || switchName.Equals("terminalLogger", comp) ||
            switchName.Equals("ll", comp) || switchName.Equals("livelogger", comp))
        {
            if (!hasValue)
            {
                loggerArg = $"{prefix}{switchName}:auto";
                return true;
            }

            if (string.IsNullOrEmpty(switchValue) ||
                !(switchValue.Equals("on", comp) ||
                  switchValue.Equals("off", comp) ||
                  switchValue.Equals("true", comp) ||
                  switchValue.Equals("false", comp) ||
                  switchValue.Equals("auto", comp)))
            {
                loggerArg = null;
                return false;
            }

            return true;
        }

        if (switchName.Equals("tlp", comp) || switchName.Equals("terminalLoggerParameters", comp) ||
            switchName.Equals("clp", comp) || switchName.Equals("consoleLoggerParameters", comp))
        {
            if (hasValue && switchValue is not "")
            {
                return true;
            }
        }

        loggerArg = null;
        return false;
    }

    private static bool TryParseSwitch(string arg, [NotNullWhen(true)] out string? prefix, [NotNullWhen(true)] out string? switchName, out string? switchValue, out bool hasValue)
    {
        prefix = null;
        switchName = null;
        switchValue = null;
        hasValue = false;

        string value;
        if (arg.StartsWith("--", StringComparison.Ordinal))
        {
            prefix = "--";
            value = arg[2..];
        }
        else if (arg.StartsWith('-') || arg.StartsWith('/'))
        {
            prefix = arg[..1];
            value = arg[1..];
        }
        else
        {
            return false;
        }

        var separatorIndex = value.IndexOf(':');
        hasValue = separatorIndex >= 0;
        switchName = hasValue ? value[..separatorIndex] : value;
        switchValue = hasValue ? value[(separatorIndex + 1)..] : null;

        return switchName.Length > 0;
    }
}

/// <summary>
/// This class acts as a wrapper around the BinaryLogger, to allow us to keep the BinaryLogger alive across multiple phases of the build.
/// The methods here are stubs so that the real binarylogger sees that we support these functionalities.
/// We need to ensure that the child logger is Initialized and Shutdown only once, so this fake event source
/// acts as a buffer. We'll provide this dispatcher to another fake logger, and that logger will
/// bind to this dispatcher to foward events from the actual build to the binary logger through this dispatcher.
/// </summary>
/// <param name="innerLogger"></param>
internal class PersistentDispatcher : EventArgsDispatcher, IEventSource4
{
    private readonly List<BinaryLogger> innerLoggers;

    public PersistentDispatcher(List<BinaryLogger> innerLoggers)
    {
        this.innerLoggers = innerLoggers;
        foreach (var logger in innerLoggers)
        {
            logger.Initialize(this);
        }
    }
    public event TelemetryEventHandler TelemetryLogged { add { } remove { } }

    public void IncludeEvaluationMetaprojects() { }
    public void IncludeEvaluationProfiles() { }
    public void IncludeEvaluationPropertiesAndItems() { }
    public void IncludeTaskInputs() { }

    public void Destroy()
    {
        foreach (var innerLogger in innerLoggers)
        {
            innerLogger.Shutdown();
        }
    }
}

/// <summary>
/// This logger acts as a forwarder to the provided dispatcher, so that multiple different build engine operations
/// can be forwarded to the shared binary logger held by the dispatcher.
/// We opt into lots of data to ensure that we can forward all events to the binary logger.
/// </summary>
/// <param name="dispatcher"></param>
internal class FacadeLogger(PersistentDispatcher dispatcher) : ILogger
{
    public PersistentDispatcher Dispatcher => dispatcher;

    public LoggerVerbosity Verbosity { get => LoggerVerbosity.Diagnostic; set { } }
    public string? Parameters { get => ""; set { } }

    public void Initialize(IEventSource eventSource)
    {
        if (eventSource is IEventSource3 eventSource3)
        {
            eventSource3.IncludeEvaluationMetaprojects();
            dispatcher.IncludeEvaluationMetaprojects();

            eventSource3.IncludeEvaluationProfiles();
            dispatcher.IncludeEvaluationProfiles();

            eventSource3.IncludeTaskInputs();
            dispatcher.IncludeTaskInputs();
        }

        eventSource.AnyEventRaised += (sender, args) => dispatcher.Dispatch(args);

        if (eventSource is IEventSource4 eventSource4)
        {
            eventSource4.IncludeEvaluationPropertiesAndItems();
            dispatcher.IncludeEvaluationPropertiesAndItems();
        }
    }

    public void ReallyShutdown()
    {
        dispatcher.Destroy();
    }

    // we don't do anything on shutdown, because we want to keep the dispatcher alive for the next phase
    public void Shutdown()
    {
    }
}
