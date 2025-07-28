// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    }

    internal static class EmojiExtensions
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
                _ => throw new InvalidOperationException()
            };
    }

    internal readonly record struct MessageDescriptor(string Format, Emoji Emoji, MessageSeverity Severity, int? Id)
    {
        private static int s_id;
        private static ImmutableDictionary<EventId, MessageDescriptor> s_descriptors = [];

        private static EventId Create(string format, Emoji emoji, MessageSeverity severity)
        {
            var id = new EventId(s_id++);
            s_descriptors = s_descriptors.Add(id, new MessageDescriptor(format, emoji, severity, id.Id));
            return id;
        }

        public static MessageDescriptor GetDescriptor(EventId id)
            => s_descriptors[id];

        public string GetMessage(string? prefix, object?[] args)
            => prefix + (Id == null ? Format : string.Format(Format, args));

        public MessageDescriptor WithSeverityWhen(MessageSeverity severity, bool condition)
            => condition ? this with { Severity = severity, Emoji = severity switch { MessageSeverity.Error => Emoji.Error, MessageSeverity.Warning => Emoji.Warning, _ => Emoji } } : this;

        // predefined messages used for testing:
        public static readonly EventId HotReloadSessionStarting = Create("Hot reload session starting.", Emoji.HotReload, MessageSeverity.None);
        public static readonly EventId HotReloadSessionStarted = Create("Hot reload session started.", Emoji.HotReload, MessageSeverity.Verbose);
        public static readonly EventId ProjectsRebuilt = Create("Projects rebuilt ({0})", Emoji.HotReload, MessageSeverity.Verbose);
        public static readonly EventId ProjectsRestarted = Create("Projects restarted ({0})", Emoji.HotReload, MessageSeverity.Verbose);
        public static readonly EventId ProjectDependenciesDeployed = Create("Project dependencies deployed ({0})", Emoji.HotReload, MessageSeverity.Verbose);
        public static readonly EventId FixBuildError = Create("Fix the error to continue or press Ctrl+C to exit.", Emoji.Watch, MessageSeverity.Warning);
        public static readonly EventId WaitingForChanges = Create("Waiting for changes", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly EventId LaunchedProcess = Create("Launched '{0}' with arguments '{1}': process id {2}", Emoji.Launch, MessageSeverity.Verbose);
        public static readonly EventId HotReloadChangeHandled = Create("Hot reload change handled in {0}ms.", Emoji.HotReload, MessageSeverity.Verbose);
        public static readonly EventId HotReloadSucceeded = Create("Hot reload succeeded.", Emoji.HotReload, MessageSeverity.Output);
        public static readonly EventId UpdatesApplied = Create("Updates applied: {0} out of {1}.", Emoji.HotReload, MessageSeverity.Verbose);
        public static readonly EventId WaitingForFileChangeBeforeRestarting = Create("Waiting for a file to change before restarting ...", Emoji.Wait, MessageSeverity.Warning);
        public static readonly EventId WatchingWithHotReload = Create("Watching with Hot Reload.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly EventId RestartInProgress = Create("Restart in progress.", Emoji.Restart, MessageSeverity.Output);
        public static readonly EventId RestartRequested = Create("Restart requested.", Emoji.Restart, MessageSeverity.Output);
        public static readonly EventId ShutdownRequested = Create("Shutdown requested. Press Ctrl+C again to force exit.", Emoji.Stop, MessageSeverity.Output);
        public static readonly EventId ApplyUpdate_Error = Create("{0}", Emoji.Error, MessageSeverity.Error);
        public static readonly EventId ApplyUpdate_Warning = Create("{0}", Emoji.Warning, MessageSeverity.Warning);
        public static readonly EventId ApplyUpdate_Verbose = Create("{0}", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly EventId ApplyUpdate_ChangingEntryPoint = Create("{0} Press \"Ctrl + R\" to restart.", Emoji.Warning, MessageSeverity.Warning);
        public static readonly EventId ApplyUpdate_FileContentDoesNotMatchBuiltSource = Create("{0} Expected if a source file is updated that is linked to project whose build is not up-to-date.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly EventId ConfiguredToLaunchBrowser = Create("dotnet-watch is configured to launch a browser on ASP.NET Core application startup.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly EventId ConfiguredToUseBrowserRefresh = Create("Configuring the app to use browser-refresh middleware", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly EventId IgnoringChangeInHiddenDirectory = Create("Ignoring change in hidden directory '{0}': {1} '{2}'", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly EventId IgnoringChangeInOutputDirectory = Create("Ignoring change in output directory: {0} '{1}'", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly EventId IgnoringChangeInExcludedFile = Create("Ignoring change in excluded file '{0}': {1}. Path matches {2} glob '{3}' set in '{4}'.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly EventId FileAdditionTriggeredReEvaluation = Create("File addition triggered re-evaluation.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly EventId ReEvaluationCompleted = Create("Re-evaluation completed.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly EventId ProjectChangeTriggeredReEvaluation = Create("Project change triggered re-evaluation.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly EventId NoCSharpChangesToApply = Create("No C# changes to apply.", Emoji.Watch, MessageSeverity.Output);
        public static readonly EventId Exited = Create("Exited", Emoji.Watch, MessageSeverity.Output);
        public static readonly EventId ExitedWithUnknownErrorCode = Create("Exited with unknown error code", Emoji.Error, MessageSeverity.Error);
        public static readonly EventId ExitedWithErrorCode = Create("Exited with error code {0}", Emoji.Error, MessageSeverity.Error);
        public static readonly EventId SkippingConfiguringBrowserRefresh_SuppressedViaEnvironmentVariable = Create("Skipping configuring browser-refresh middleware since its refresh server suppressed via environment variable {0}.", Emoji.Watch, MessageSeverity.Verbose);
        public static readonly EventId SkippingConfiguringBrowserRefresh_TargetFrameworkNotSupported = Create("Skipping configuring browser-refresh middleware since the target framework version is not supported. For more information see 'https://aka.ms/dotnet/watch/unsupported-tfm'.", Emoji.Watch, MessageSeverity.Warning);
        public static readonly EventId SkippingConfiguringBrowserRefresh_NotWebApp = Create("Skipping configuring browser-refresh middleware since this is not a webapp.", Emoji.Watch, MessageSeverity.Verbose);
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

        void Report(EventId eventId, string prefix, params object?[] args)
            => Report(MessageDescriptor.GetDescriptor(eventId), prefix, args);

        void Report(EventId eventId, params object?[] args)
            => Report(eventId, prefix: "", args);

        void ReportAs(EventId eventId, MessageSeverity severity, bool when, params object?[] args)
            => Report(MessageDescriptor.GetDescriptor(eventId).WithSeverityWhen(severity, when), prefix: "", args);

        void Verbose(string message, Emoji emoji = Emoji.Watch)
            => Report(new MessageDescriptor(message, emoji, MessageSeverity.Verbose, Id: null), prefix: "", args: []);

        void Output(string message, Emoji emoji = Emoji.Watch)
            => Report(new MessageDescriptor(message, emoji, MessageSeverity.Output, Id: null), prefix: "", args: []);

        void Warn(string message)
            => Report(new MessageDescriptor(message, Emoji.Warning, MessageSeverity.Warning, Id: null), prefix: "", args: []);

        void Error(string message)
            => Report(new MessageDescriptor(message, Emoji.Error, MessageSeverity.Error, Id: null), prefix: "", args: []);
    }
}
