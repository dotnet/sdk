// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch
{
    internal enum MessageSeverity
    {
        None,
        Verbose,
        Output,
        Warning,
        Error,
    }

    internal enum Emoji
    {
        Default = 0,

        Warning,
        Error,
        HotReload,
        Watch,
        Stop,
        Restart,
        Launch,
        Wait,
        Aspire,
        Browser,
        Agent,
        Build,
        Refresh,
    }

    internal static class Extensions
    {
        public static string ToDisplay(this Emoji emoji)
            => emoji switch
            {
                Emoji.Warning => "⚠",
                Emoji.Error => "❌",
                Emoji.HotReload => "🔥",
                Emoji.Watch => "⌚",
                Emoji.Stop => "🛑",
                Emoji.Restart => "🔄",
                Emoji.Launch => "🚀",
                Emoji.Wait => "⏳",
                Emoji.Aspire => "⭐",
                Emoji.Browser => "🌐",
                Emoji.Agent => "🕵️",
                Emoji.Build => "🔨",
                Emoji.Refresh => "🔃",
                _ => throw new InvalidOperationException()
            };

        public static void Log(this ILogger logger, EventId eventId, params object?[] args)
        {
            var descriptor = MessageDescriptor.GetDescriptor(eventId);

            logger.Log(
                descriptor.Severity.ToLogLevel(),
                eventId,
                state: (descriptor, args),
                exception: null,
                formatter: static (state, _) => state.descriptor.GetMessage(prefix: "", state.args));
        }

        public static LogLevel ToLogLevel(this MessageSeverity severity)
            => severity switch
            {
                MessageSeverity.Verbose => LogLevel.Debug,
                MessageSeverity.Output => LogLevel.Information,
                MessageSeverity.Warning => LogLevel.Warning,
                MessageSeverity.Error => LogLevel.Error,
                _ => throw new InvalidOperationException()
            };

        public static MessageSeverity ToSeverity(this LogLevel level)
            => level switch
            {
                LogLevel.Debug => MessageSeverity.Verbose,
                LogLevel.Information => MessageSeverity.Output,
                LogLevel.Warning => MessageSeverity.Warning,
                LogLevel.Error => MessageSeverity.Error,
                _ => throw new InvalidOperationException()
            };
    }

    internal sealed class LoggerFactory(IReporter reporter) : ILoggerFactory
    {
        private sealed class Logger(IReporter reporter, string categoryName) : ILogger
        {
            public bool IsEnabled(LogLevel logLevel)
                => reporter.IsVerbose || logLevel > LogLevel.Debug;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var (name, display) = LoggingUtilities.ParseCategoryName(categoryName);
                var prefix = display != null ? $"[{display}] " : "";

                var severity = logLevel.ToSeverity();
                var descriptor = eventId.Id != 0 ? MessageDescriptor.GetDescriptor(eventId) : default;

                var emoji = severity switch
                {
                    MessageSeverity.Error => Emoji.Error,
                    MessageSeverity.Warning => Emoji.Warning,
                    _ when descriptor.Emoji != Emoji.Default => descriptor.Emoji,
                    _ when MessageDescriptor.ComponentEmojis.TryGetValue(name, out var componentEmoji) => componentEmoji,
                    _ => Emoji.Watch
                };

                object?[] args;
                if (eventId.Id == 0)
                {
                    // ad-hoc message
                    descriptor = new MessageDescriptor(formatter(state, exception), emoji, severity, Id: default);
                    args = [];
                }
                else
                {
                    args = state is IReadOnlyList<KeyValuePair<string, object?>> namedArgs ? [.. namedArgs.Select(na => na.Value)] : [];
                }

                reporter.Report(descriptor, prefix, args);
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
                => throw new NotImplementedException();
        }

        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName)
            => new Logger(reporter, categoryName);

        public void AddProvider(ILoggerProvider provider)
            => throw new NotImplementedException();
    }

    internal readonly record struct MessageDescriptor(string Format, Emoji Emoji, MessageSeverity Severity, EventId Id)
    {
        private static int s_id;
        private static ImmutableDictionary<EventId, MessageDescriptor> s_descriptors = [];

        private static MessageDescriptor Create(string format, Emoji emoji, MessageSeverity severity)
            // reserve event id 0 for ad-hoc messages
            => Create(new EventId(++s_id), format, emoji, severity);

        private static MessageDescriptor Create(LogEvent logEvent, Emoji emoji)
            => Create(logEvent.Id, logEvent.Message, emoji, logEvent.Level.ToSeverity());

        private static MessageDescriptor Create(EventId id, string format, Emoji emoji, MessageSeverity severity)
        {
            var descriptor = new MessageDescriptor(format, emoji, severity, id.Id);
            s_descriptors = s_descriptors.Add(id, descriptor);
            return descriptor;
        }

        public static MessageDescriptor GetDescriptor(EventId id)
            => s_descriptors[id];

        public string GetMessage(string? prefix, object?[] args)
            => prefix + (Id.Id == 0 ? Format : string.Format(Format, args));

        public MessageDescriptor WithSeverityWhen(MessageSeverity severity, bool condition)
            => condition && Severity != severity
                ? this with { Severity = severity, Emoji = severity switch { MessageSeverity.Error => Emoji.Error, MessageSeverity.Warning => Emoji.Warning, _ => Emoji } }
                : this;

        public static readonly ImmutableDictionary<string, Emoji> ComponentEmojis = ImmutableDictionary<string, Emoji>.Empty
            .Add(HotReloadDotNetWatcher.ClientLogComponentName, Emoji.HotReload)
            .Add(HotReloadDotNetWatcher.AgentLogComponentName, Emoji.Agent)
            .Add(BrowserRefreshServer.ServerLogComponentName, Emoji.Refresh)
            .Add(BrowserConnection.AgentLogComponentName, Emoji.Agent)
            .Add(BrowserConnection.ServerLogComponentName, Emoji.Browser);

        // predefined messages used for testing:
        public static readonly MessageDescriptor HotReloadSessionStarting = Create("Hot reload session starting.", Emoji.HotReload, MessageSeverity.None);
        public static readonly MessageDescriptor HotReloadSessionStarted = Create("Hot reload session started.", Emoji.HotReload, MessageSeverity.Verbose);
        public static readonly MessageDescriptor ProjectsRebuilt = Create("Projects rebuilt ({0})", Emoji.HotReload, MessageSeverity.Verbose);
        public static readonly MessageDescriptor ProjectsRestarted = Create("Projects restarted ({0})", Emoji.HotReload, MessageSeverity.Verbose);
        public static readonly MessageDescriptor ProjectDependenciesDeployed = Create("Project dependencies deployed ({0})", Emoji.HotReload, MessageSeverity.Verbose);
        public static readonly MessageDescriptor FixBuildError = Create("Fix the error to continue or press Ctrl+C to exit.", Emoji.Watch, MessageSeverity.Warning);
        public static readonly MessageDescriptor WaitingForChanges = Create("Waiting for changes", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor LaunchedProcess = Create("Launched '{0}' with arguments '{1}': process id {2}", Emoji.Launch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor HotReloadChangeHandled = Create("Hot reload change handled in {0}ms.", Emoji.HotReload, MessageSeverity.Verbose);
        public static readonly MessageDescriptor HotReloadSucceeded = Create("Hot reload succeeded.", Emoji.HotReload, MessageSeverity.Output);
        public static readonly MessageDescriptor UpdatesApplied = Create(LogEvents.UpdatesApplied, Emoji.HotReload);
        public static readonly MessageDescriptor WaitingForFileChangeBeforeRestarting = Create("Waiting for a file to change before restarting ...", Emoji.Wait, MessageSeverity.Warning);
        public static readonly MessageDescriptor WatchingWithHotReload = Create("Watching with Hot Reload.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor RestartInProgress = Create("Restart in progress.", Emoji.Restart, MessageSeverity.Output);
        public static readonly MessageDescriptor RestartRequested = Create("Restart requested.", Emoji.Restart, MessageSeverity.Output);
        public static readonly MessageDescriptor ShutdownRequested = Create("Shutdown requested. Press Ctrl+C again to force exit.", Emoji.Stop, MessageSeverity.Output);
        public static readonly MessageDescriptor ApplyUpdate_Error = Create("{0}", Emoji.Error, MessageSeverity.Error);
        public static readonly MessageDescriptor ApplyUpdate_Warning = Create("{0}", Emoji.Warning, MessageSeverity.Warning);
        public static readonly MessageDescriptor ApplyUpdate_Verbose = Create("{0}", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor ApplyUpdate_ChangingEntryPoint = Create("{0} Press \"Ctrl + R\" to restart.", Emoji.Warning, MessageSeverity.Warning);
        public static readonly MessageDescriptor ApplyUpdate_FileContentDoesNotMatchBuiltSource = Create("{0} Expected if a source file is updated that is linked to project whose build is not up-to-date.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor ConfiguredToLaunchBrowser = Create("dotnet-watch is configured to launch a browser on ASP.NET Core application startup.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor ConfiguredToUseBrowserRefresh = Create("Configuring the app to use browser-refresh middleware", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor IgnoringChangeInHiddenDirectory = Create("Ignoring change in hidden directory '{0}': {1} '{2}'", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor IgnoringChangeInOutputDirectory = Create("Ignoring change in output directory: {0} '{1}'", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor IgnoringChangeInExcludedFile = Create("Ignoring change in excluded file '{0}': {1}. Path matches {2} glob '{3}' set in '{4}'.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor FileAdditionTriggeredReEvaluation = Create("File addition triggered re-evaluation.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor ReEvaluationCompleted = Create("Re-evaluation completed.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor ProjectChangeTriggeredReEvaluation = Create("Project change triggered re-evaluation.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor NoCSharpChangesToApply = Create("No C# changes to apply.", Emoji.Watch, MessageSeverity.Output);
        public static readonly MessageDescriptor Exited = Create("Exited", Emoji.Watch, MessageSeverity.Output);
        public static readonly MessageDescriptor ExitedWithUnknownErrorCode = Create("Exited with unknown error code", Emoji.Error, MessageSeverity.Error);
        public static readonly MessageDescriptor ExitedWithErrorCode = Create("Exited with error code {0}", Emoji.Error, MessageSeverity.Error);
        public static readonly MessageDescriptor SkippingConfiguringBrowserRefresh_SuppressedViaEnvironmentVariable = Create("Skipping configuring browser-refresh middleware since its refresh server suppressed via environment variable {0}.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor SkippingConfiguringBrowserRefresh_TargetFrameworkNotSupported = Create("Skipping configuring browser-refresh middleware since the target framework version is not supported. For more information see 'https://aka.ms/dotnet/watch/unsupported-tfm'.", Emoji.Watch, MessageSeverity.Warning);
        public static readonly MessageDescriptor SkippingConfiguringBrowserRefresh_NotWebApp = Create("Skipping configuring browser-refresh middleware since this is not a webapp.", Emoji.Watch, MessageSeverity.Verbose);
    }

    internal interface IReporter
    {
        void Report(MessageDescriptor descriptor, string prefix, object?[] args);

        public bool IsVerbose
            => false;

        /// <summary>
        /// If true, the output of the process will be prefixed with the project display name.
        /// Used for testing.
        /// </summary>
        public bool PrefixProcessOutput
            => false;

        /// <summary>
        /// Reports the output of a process that is being watched.
        /// </summary>
        /// <remarks>
        /// Not used to report output of dotnet-build processed launched by dotnet-watch to build or evaluate projects.
        /// </remarks>
        void ReportProcessOutput(OutputLine line);

        void ReportWithPrefix(MessageDescriptor descriptor, string prefix, params object?[] args)
            => Report(descriptor, prefix, args);

        void Report(MessageDescriptor descriptor, params object?[] args)
            => ReportWithPrefix(descriptor, prefix: "", args);

        void ReportAs(MessageDescriptor descriptor, MessageSeverity severity, bool when, params object?[] args)
            => Report(descriptor.WithSeverityWhen(severity, when), prefix: "", args);

        void Report(string message, Emoji emoji, MessageSeverity severity)
            => Report(new MessageDescriptor(message, emoji, severity, Id: default), prefix: "", args: []);

        void Verbose(string message, Emoji emoji = Emoji.Watch)
            => Report(message, emoji, MessageSeverity.Verbose);

        void Output(string message, Emoji emoji = Emoji.Watch)
            => Report(message, emoji, MessageSeverity.Output);

        void Warn(string message)
            => Report(message, Emoji.Warning, MessageSeverity.Warning);

        void Error(string message)
            => Report(message, Emoji.Error, MessageSeverity.Error);
    }
}
