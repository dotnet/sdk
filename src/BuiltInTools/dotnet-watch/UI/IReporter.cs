// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

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

    internal readonly record struct MessageDescriptor(string? Format, string? Emoji, MessageSeverity Severity, int? Id)
    {
        private static readonly int s_id;

        [MemberNotNullWhen(true, nameof(Format), nameof(Emoji))]
        public bool HasMessage
            => Severity != MessageSeverity.None;

        [MemberNotNullWhen(true, nameof(Format), nameof(Emoji))]
        public bool TryGetMessage(string? prefix, object?[] args, [NotNullWhen(true)] out string? message)
        {
            // Messages without Id are created by IReporter.Verbose|Output|Warn|Error helpers.
            // They do not have arguments and we shouldn't interpret Format as a string with holes.
            // Eventually, all messages should have a descriptor (so we can localize them) and this can be removed.
            if (Id == null)
            {
                Debug.Assert(args is null or []);
                Debug.Assert(HasMessage);
                message = prefix + Format;
                return true;
            }

            if (!HasMessage)
            {
                message = null;
                return false;
            }


            message = prefix + string.Format(Format, args);
            return true;
        }

        public const string WarningEmoji = "⚠";
        public const string ErrorEmoji = "❌";
        public const string HotReloadEmoji = "🔥";
        public const string WatchEmoji = "⌚";
        public const string StopEmoji = "🛑";
        public const string RestartEmoji = "🔄";
        public const string LaunchEmoji = "🚀";
        public const string WaitEmoji = "⏳";

        public MessageDescriptor ToErrorWhen(bool condition)
            => condition ? this with { Severity = MessageSeverity.Error, Emoji = ErrorEmoji } : this;

        // predefined messages used for testing:
        public static readonly MessageDescriptor HotReloadSessionStarting = new(Format: null, Emoji: null, MessageSeverity.None, s_id++);
        public static readonly MessageDescriptor HotReloadSessionStarted = new("Hot reload session started.", HotReloadEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ProjectsRebuilt = new("Projects rebuilt ({0})", HotReloadEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ProjectsRestarted = new("Projects restarted ({0})", HotReloadEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ProjectDependenciesDeployed = new("Project dependencies deployed ({0})", HotReloadEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor FixBuildError = new("Fix the error to continue or press Ctrl+C to exit.", WatchEmoji, MessageSeverity.Warning, s_id++);
        public static readonly MessageDescriptor WaitingForChanges = new("Waiting for changes", WatchEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor LaunchedProcess = new("Launched '{0}' with arguments '{1}': process id {2}", LaunchEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor HotReloadChangeHandled = new("Hot reload change handled in {0}ms.", HotReloadEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor HotReloadSucceeded = new("Hot reload succeeded.", HotReloadEmoji, MessageSeverity.Output, s_id++);
        public static readonly MessageDescriptor UpdatesApplied = new("Updates applied: {0} out of {1}.", HotReloadEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor WaitingForFileChangeBeforeRestarting = new("Waiting for a file to change before restarting ...", WaitEmoji, MessageSeverity.Output, s_id++);
        public static readonly MessageDescriptor WatchingWithHotReload = new("Watching with Hot Reload.", WatchEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor RestartInProgress = new("Restart in progress.", RestartEmoji, MessageSeverity.Output, s_id++);
        public static readonly MessageDescriptor RestartRequested = new("Restart requested.", RestartEmoji, MessageSeverity.Output, s_id++);
        public static readonly MessageDescriptor ShutdownRequested = new("Shutdown requested. Press Ctrl+C again to force exit.", StopEmoji, MessageSeverity.Output, s_id++);
        public static readonly MessageDescriptor ApplyUpdate_Error = new("{0}", ErrorEmoji, MessageSeverity.Error, s_id++);
        public static readonly MessageDescriptor ApplyUpdate_Warning = new("{0}", WarningEmoji, MessageSeverity.Warning, s_id++);
        public static readonly MessageDescriptor ApplyUpdate_Verbose = new("{0}", WatchEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ApplyUpdate_ChangingEntryPoint = new("{0} Press \"Ctrl + R\" to restart.", WarningEmoji, MessageSeverity.Warning, s_id++);
        public static readonly MessageDescriptor ApplyUpdate_FileContentDoesNotMatchBuiltSource = new("{0} Expected if a source file is updated that is linked to project whose build is not up-to-date.", WatchEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ConfiguredToLaunchBrowser = new("dotnet-watch is configured to launch a browser on ASP.NET Core application startup.", WatchEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ConfiguredToUseBrowserRefresh = new("Configuring the app to use browser-refresh middleware", WatchEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor IgnoringChangeInHiddenDirectory = new("Ignoring change in hidden directory '{0}': {1} '{2}'", WatchEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor IgnoringChangeInOutputDirectory = new("Ignoring change in output directory: {0} '{1}'", WatchEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor IgnoringChangeInExcludedFile = new("Ignoring change in excluded file '{0}': {1}. Path matches {2} glob '{3}' set in '{4}'.", WatchEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor FileAdditionTriggeredReEvaluation = new("File addition triggered re-evaluation.", WatchEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ReEvaluationCompleted = new("Re-evaluation completed.", WatchEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ProjectChangeTriggeredReEvaluation = new("Project change triggered re-evaluation.", WatchEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor NoCSharpChangesToApply = new("No C# changes to apply.", WatchEmoji, MessageSeverity.Output, s_id++);
        public static readonly MessageDescriptor Exited = new("Exited", WatchEmoji, MessageSeverity.Output, s_id++);
        public static readonly MessageDescriptor ExitedWithUnknownErrorCode = new("Exited with unknown error code", ErrorEmoji, MessageSeverity.Error, s_id++);
        public static readonly MessageDescriptor ExitedWithErrorCode = new("Exited with error code {0}", ErrorEmoji, MessageSeverity.Error, s_id++);
        public static readonly MessageDescriptor SkippingConfiguringBrowserRefresh_SuppressedViaEnvironmentVariable = new("Skipping configuring browser-refresh middleware since its refresh server suppressed via environment variable {0}.", WatchEmoji, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor SkippingConfiguringBrowserRefresh_TargetFrameworkNotSupported = new("Skipping configuring browser-refresh middleware since the target framework version is not supported. For more information see 'https://aka.ms/dotnet/watch/unsupported-tfm'.", WatchEmoji, MessageSeverity.Warning, s_id++);
        public static readonly MessageDescriptor SkippingConfiguringBrowserRefresh_NotWebApp = new("Skipping configuring browser-refresh middleware since this is not a webapp.", WatchEmoji, MessageSeverity.Verbose, s_id++);
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

        void Report(MessageDescriptor descriptor, params object?[] args)
            => Report(descriptor, prefix: "", args);

        void Verbose(string message, string emoji = MessageDescriptor.WatchEmoji)
            => Report(new MessageDescriptor(message, emoji, MessageSeverity.Verbose, Id: null));

        void Output(string message, string emoji = MessageDescriptor.WatchEmoji)
            => Report(new MessageDescriptor(message, emoji, MessageSeverity.Output, Id: null));

        void Warn(string message, string emoji = MessageDescriptor.WatchEmoji)
            => Report(new MessageDescriptor(message, emoji, MessageSeverity.Warning, Id: null));

        void Error(string message, string emoji = MessageDescriptor.ErrorEmoji)
            => Report(new MessageDescriptor(message, emoji, MessageSeverity.Error, Id: null));
    }
}
