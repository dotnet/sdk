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
        private static readonly int s_id;

        public bool HasMessage
            => Severity != MessageSeverity.None;

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

        public MessageDescriptor ToErrorWhen(bool condition)
            => condition ? this with { Severity = MessageSeverity.Error, Emoji = Emoji.Error } : this;

        // predefined messages used for testing:
        public static readonly MessageDescriptor HotReloadSessionStarting = new("Hot reload session starting.", Emoji.HotReload, MessageSeverity.None, s_id++);
        public static readonly MessageDescriptor HotReloadSessionStarted = new("Hot reload session started.", Emoji.HotReload, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ProjectsRebuilt = new("Projects rebuilt ({0})", Emoji.HotReload, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ProjectsRestarted = new("Projects restarted ({0})", Emoji.HotReload, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ProjectDependenciesDeployed = new("Project dependencies deployed ({0})", Emoji.HotReload, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor FixBuildError = new("Fix the error to continue or press Ctrl+C to exit.", Emoji.Watch, MessageSeverity.Warning, s_id++);
        public static readonly MessageDescriptor WaitingForChanges = new("Waiting for changes", Emoji.Watch, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor LaunchedProcess = new("Launched '{0}' with arguments '{1}': process id {2}", Emoji.Launch, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor HotReloadChangeHandled = new("Hot reload change handled in {0}ms.", Emoji.HotReload, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor HotReloadSucceeded = new("Hot reload succeeded.", Emoji.HotReload, MessageSeverity.Output, s_id++);
        public static readonly MessageDescriptor UpdatesApplied = new("Updates applied: {0} out of {1}.", Emoji.HotReload, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor WaitingForFileChangeBeforeRestarting = new("Waiting for a file to change before restarting ...", Emoji.Wait, MessageSeverity.Warning, s_id++);
        public static readonly MessageDescriptor WatchingWithHotReload = new("Watching with Hot Reload.", Emoji.Watch, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor RestartInProgress = new("Restart in progress.", Emoji.Restart, MessageSeverity.Output, s_id++);
        public static readonly MessageDescriptor RestartRequested = new("Restart requested.", Emoji.Restart, MessageSeverity.Output, s_id++);
        public static readonly MessageDescriptor ShutdownRequested = new("Shutdown requested. Press Ctrl+C again to force exit.", Emoji.Stop, MessageSeverity.Output, s_id++);
        public static readonly MessageDescriptor ApplyUpdate_Error = new("{0}", Emoji.Error, MessageSeverity.Error, s_id++);
        public static readonly MessageDescriptor ApplyUpdate_Warning = new("{0}", Emoji.Warning, MessageSeverity.Warning, s_id++);
        public static readonly MessageDescriptor ApplyUpdate_Verbose = new("{0}", Emoji.Watch, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ApplyUpdate_ChangingEntryPoint = new("{0} Press \"Ctrl + R\" to restart.", Emoji.Warning, MessageSeverity.Warning, s_id++);
        public static readonly MessageDescriptor ApplyUpdate_FileContentDoesNotMatchBuiltSource = new("{0} Expected if a source file is updated that is linked to project whose build is not up-to-date.", Emoji.Watch, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ConfiguredToLaunchBrowser = new("dotnet-watch is configured to launch a browser on ASP.NET Core application startup.", Emoji.Watch, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ConfiguredToUseBrowserRefresh = new("Configuring the app to use browser-refresh middleware", Emoji.Watch, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor IgnoringChangeInHiddenDirectory = new("Ignoring change in hidden directory '{0}': {1} '{2}'", Emoji.Watch, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor IgnoringChangeInOutputDirectory = new("Ignoring change in output directory: {0} '{1}'", Emoji.Watch, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor IgnoringChangeInExcludedFile = new("Ignoring change in excluded file '{0}': {1}. Path matches {2} glob '{3}' set in '{4}'.", Emoji.Watch, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor FileAdditionTriggeredReEvaluation = new("File addition triggered re-evaluation.", Emoji.Watch, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ReEvaluationCompleted = new("Re-evaluation completed.", Emoji.Watch, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ProjectChangeTriggeredReEvaluation = new("Project change triggered re-evaluation.", Emoji.Watch, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor NoCSharpChangesToApply = new("No C# changes to apply.", Emoji.Watch, MessageSeverity.Output, s_id++);
        public static readonly MessageDescriptor Exited = new("Exited", Emoji.Watch, MessageSeverity.Output, s_id++);
        public static readonly MessageDescriptor ExitedWithUnknownErrorCode = new("Exited with unknown error code", Emoji.Error, MessageSeverity.Error, s_id++);
        public static readonly MessageDescriptor ExitedWithErrorCode = new("Exited with error code {0}", Emoji.Error, MessageSeverity.Error, s_id++);
        public static readonly MessageDescriptor SkippingConfiguringBrowserRefresh_SuppressedViaEnvironmentVariable = new("Skipping configuring browser-refresh middleware since its refresh server suppressed via environment variable {0}.", Emoji.Watch, MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor SkippingConfiguringBrowserRefresh_TargetFrameworkNotSupported = new("Skipping configuring browser-refresh middleware since the target framework version is not supported. For more information see 'https://aka.ms/dotnet/watch/unsupported-tfm'.", Emoji.Watch, MessageSeverity.Warning, s_id++);
        public static readonly MessageDescriptor SkippingConfiguringBrowserRefresh_NotWebApp = new("Skipping configuring browser-refresh middleware since this is not a webapp.", Emoji.Watch, MessageSeverity.Verbose, s_id++);
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

        void Verbose(string message, Emoji emoji = Emoji.Watch)
            => Report(new MessageDescriptor(message, emoji, MessageSeverity.Verbose, Id: null));

        void Output(string message, Emoji emoji = Emoji.Watch)
            => Report(new MessageDescriptor(message, emoji, MessageSeverity.Output, Id: null));

        void Warn(string message)
            => Report(new MessageDescriptor(message, Emoji.Warning, MessageSeverity.Warning, Id: null));

        void Error(string message)
            => Report(new MessageDescriptor(message, Emoji.Error, MessageSeverity.Error, Id: null));
    }
}
