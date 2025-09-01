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
        LightBulb,
    }

    internal static class Extensions
    {
        public static string ToDisplay(this Emoji emoji)
            => emoji switch
            {
                Emoji.Default => ":",
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
                Emoji.LightBulb => "💡",
                _ => throw new InvalidOperationException()
            };

        public static void Log(this ILogger logger, MessageDescriptor descriptor, params object?[] args)
        {
            logger.Log(
                descriptor.Severity.ToLogLevel(),
                descriptor.Id,
                state: (descriptor, args),
                exception: null,
                formatter: static (state, _) => state.descriptor.GetMessage(state.args));
        }

        public static LogLevel ToLogLevel(this MessageSeverity severity)
            => severity switch
            {
                MessageSeverity.None => LogLevel.None,
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
                LogLevel.None => MessageSeverity.None,
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
                    _ when descriptor.Emoji != Emoji.Default => descriptor.Emoji,
                    MessageSeverity.Error => Emoji.Error,
                    MessageSeverity.Warning => Emoji.Warning,
                    _ when MessageDescriptor.ComponentEmojis.TryGetValue(name, out var componentEmoji) => componentEmoji,
                    _ => Emoji.Watch
                };

                reporter.Report(eventId, emoji, severity, prefix + formatter(state, exception));
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

        public string GetMessage(params object?[] args)
            => Id.Id == 0 ? Format : string.Format(Format, args);

        public MessageDescriptor WithSeverityWhen(MessageSeverity severity, bool condition)
            => condition && Severity != severity
                ? this with { Severity = severity, Emoji = severity switch { MessageSeverity.Error => Emoji.Error, MessageSeverity.Warning => Emoji.Warning, _ => Emoji } }
                : this;

        public static readonly ImmutableDictionary<string, Emoji> ComponentEmojis = ImmutableDictionary<string, Emoji>.Empty
            .Add(Program.LogComponentName, Emoji.Watch)
            .Add(DotNetWatchContext.DefaultLogComponentName, Emoji.Watch)
            .Add(DotNetWatchContext.BuildLogComponentName, Emoji.Build)
            .Add(HotReloadDotNetWatcher.ClientLogComponentName, Emoji.HotReload)
            .Add(HotReloadDotNetWatcher.AgentLogComponentName, Emoji.Agent)
            .Add(BrowserRefreshServer.ServerLogComponentName, Emoji.Refresh)
            .Add(BrowserConnection.AgentLogComponentName, Emoji.Agent)
            .Add(BrowserConnection.ServerLogComponentName, Emoji.Browser)
            .Add(AspireServiceFactory.AspireLogComponentName, Emoji.Aspire);

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
        public static readonly MessageDescriptor Capabilities = Create(LogEvents.Capabilities, Emoji.HotReload);
        public static readonly MessageDescriptor WaitingForFileChangeBeforeRestarting = Create("Waiting for a file to change before restarting ...", Emoji.Wait, MessageSeverity.Warning);
        public static readonly MessageDescriptor WatchingWithHotReload = Create("Watching with Hot Reload.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor RestartInProgress = Create("Restart in progress.", Emoji.Restart, MessageSeverity.Output);
        public static readonly MessageDescriptor RestartRequested = Create("Restart requested.", Emoji.Restart, MessageSeverity.Output);
        public static readonly MessageDescriptor ShutdownRequested = Create("Shutdown requested. Press Ctrl+C again to force exit.", Emoji.Stop, MessageSeverity.Output);
        public static readonly MessageDescriptor ApplyUpdate_Error = Create("{0}{1}", Emoji.Error, MessageSeverity.Error);
        public static readonly MessageDescriptor ApplyUpdate_Warning = Create("{0}{1}", Emoji.Warning, MessageSeverity.Warning);
        public static readonly MessageDescriptor ApplyUpdate_Verbose = Create("{0}{1}", Emoji.Default, MessageSeverity.Verbose);
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
        public static readonly MessageDescriptor FailedToLaunchProcess = Create("Failed to launch '{0}' with arguments '{1}': {2}", Emoji.Error, MessageSeverity.Error);
        public static readonly MessageDescriptor ApplicationFailed = Create("Application failed: {0}", Emoji.Error, MessageSeverity.Error);
        public static readonly MessageDescriptor ProcessRunAndExited = Create("Process id {0} ran for {1}ms and exited with exit code {2}.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor WaitingForProcessToExitWithin = Create("Waiting for process {0} to exit within {1}s.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor WaitingForProcessToExit = Create("Waiting for process {0} to exit ({1}).", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor FailedToKillProcess = Create("Failed to kill process {0}: {1}.", Emoji.Error, MessageSeverity.Error);
        public static readonly MessageDescriptor TerminatingProcess = Create("Terminating process {0} ({1}).", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor FailedToSendSignalToProcess = Create("Failed to send {0} signal to process {1}: {2}", Emoji.Warning, MessageSeverity.Warning);
        public static readonly MessageDescriptor ErrorReadingProcessOutput = Create("Error reading {0} of process {1}: {2}", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor HotReloadOfScopedCssSucceeded = Create("Hot reload of scoped css succeeded.", Emoji.HotReload, MessageSeverity.Output);
        public static readonly MessageDescriptor HotReloadOfScopedCssPartiallySucceeded = Create("Hot reload of scoped css partially succeeded: {0} project(s) out of {1} were updated.", Emoji.HotReload, MessageSeverity.Output);
        public static readonly MessageDescriptor HotReloadOfScopedCssFailed = Create("Hot reload of scoped css failed.", Emoji.Error, MessageSeverity.Error);
        public static readonly MessageDescriptor HotReloadOfStaticAssetsSucceeded = Create("Hot reload of static assets succeeded.", Emoji.HotReload, MessageSeverity.Output);
        public static readonly MessageDescriptor SendingStaticAssetUpdateRequest = Create("Sending static asset update request to connected browsers: '{0}'.", Emoji.Refresh, MessageSeverity.Verbose);
        public static readonly MessageDescriptor UpdatingDiagnosticsInConnectedBrowsers = Create("Updating diagnostics in connected browsers.", Emoji.Refresh, MessageSeverity.Verbose);
        public static readonly MessageDescriptor FailedToReceiveResponseFromConnectedBrowser = Create("Failed to receive response from a connected browser.", Emoji.Refresh, MessageSeverity.Verbose);
        public static readonly MessageDescriptor HotReloadCapabilities = Create("Hot reload capabilities: {0}.", Emoji.HotReload, MessageSeverity.Verbose);
        public static readonly MessageDescriptor HotReloadSuspended = Create("Hot reload suspended. To continue hot reload, press \"Ctrl + R\".", Emoji.HotReload, MessageSeverity.Output);
        public static readonly MessageDescriptor UnableToApplyChanges = Create("Unable to apply changes due to compilation errors.", Emoji.HotReload, MessageSeverity.Output);
        public static readonly MessageDescriptor RestartNeededToApplyChanges = Create("Restart is needed to apply the changes.", Emoji.HotReload, MessageSeverity.Output);
        public static readonly MessageDescriptor HotReloadEnabled = Create("Hot reload enabled. For a list of supported edits, see https://aka.ms/dotnet/hot-reload.", Emoji.HotReload, MessageSeverity.Output);
        public static readonly MessageDescriptor PressCtrlRToRestart = Create("Press Ctrl+R to restart.", Emoji.LightBulb, MessageSeverity.Output);
        public static readonly MessageDescriptor HotReloadCanceledProcessExited = Create("Hot reload canceled because the process exited.", Emoji.HotReload, MessageSeverity.Verbose);
        public static readonly MessageDescriptor HotReloadProfile_BlazorHosted = Create("HotReloadProfile: BlazorHosted. '{0}' references BlazorWebAssembly project '{1}'.", Emoji.HotReload, MessageSeverity.Verbose);
        public static readonly MessageDescriptor HotReloadProfile_BlazorWebAssembly = Create("HotReloadProfile: BlazorWebAssembly.", Emoji.HotReload, MessageSeverity.Verbose);
        public static readonly MessageDescriptor HotReloadProfile_Default = Create("HotReloadProfile: Default.", Emoji.HotReload, MessageSeverity.Verbose);
        public static readonly MessageDescriptor WatchingFilesForChanges = Create("Watching {0} file(s) for changes", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor WatchingFilesForChanges_FilePath = Create("> {0}", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly MessageDescriptor Building = Create("Building {0} ...", Emoji.Default, MessageSeverity.Output);
        public static readonly MessageDescriptor BuildSucceeded = Create(" Build succeeded: {0}", Emoji.Default, MessageSeverity.Output);
        public static readonly MessageDescriptor BuildFailed = Create(" Build failed: {0}", Emoji.Default, MessageSeverity.Output);
    }

    internal interface IProcessOutputReporter
    {
        /// <summary>
        /// If true, the output of the process will be prefixed with the project display name.
        /// Used for testing.
        /// </summary>
        bool PrefixProcessOutput { get; }

        /// <summary>
        /// Reports the output of a process that is being watched.
        /// </summary>
        /// <remarks>
        /// Not used to report output of dotnet-build processed launched by dotnet-watch to build or evaluate projects.
        /// </remarks>
        void ReportOutput(OutputLine line);
    }

    internal interface IReporter
    {
        void Report(EventId id, Emoji emoji, MessageSeverity severity, string message);

        public bool IsVerbose
            => false;
    }
}
