﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Tasks;
using Microsoft.DotNet.Watcher;

namespace Microsoft.Extensions.Tools.Internal
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
            if (!HasMessage)
            {
                message = null;
                return false;
            }

            message = prefix + string.Format(Format, args);
            return true;
        }

        // predefined messages used for testing:
        public static readonly MessageDescriptor HotReloadSessionStarting = new(Format: null, Emoji: null, MessageSeverity.None, s_id++);
        public static readonly MessageDescriptor HotReloadSessionStarted = new("Hot reload session started.", "🔥", MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor HotReloadSessionEnded = new("Hot reload session ended.", "🔥", MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor FixBuildError = new("Fix the error to continue or press Ctrl+C to exit.", "⌚", MessageSeverity.Warning, s_id++);
        public static readonly MessageDescriptor WaitingForChanges = new("Waiting for changes", "⌚", MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor LaunchedProcess = new("Launched '{0}' with arguments '{1}': process id {2}", "🚀", MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor KillingProcess = new("Killing process {0}", "⌚", MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor HotReloadChangeHandled = new("Hot reload change handled in {0}ms.", "🔥", MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor HotReloadSucceeded = new("Hot reload succeeded.", "🔥", MessageSeverity.Output, s_id++);
        public static readonly MessageDescriptor BuildCompleted = new("Build completed.", "⌚", MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor UpdatesApplied = new("Updates applied: {0} out of {1}.", "🔥", MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor WaitingForFileChangeBeforeRestarting = new("Waiting for a file to change before restarting dotnet...", "⏳", MessageSeverity.Warning, s_id++);
        public static readonly MessageDescriptor WatchingWithHotReload = new("Watching with Hot Reload.", "⌚", MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor RestartInProgress = new("Restart in progress.", "🔄", MessageSeverity.Output, s_id++);
        public static readonly MessageDescriptor RestartRequested = new("Restart requested.", "🔄", MessageSeverity.Output, s_id++);
        public static readonly MessageDescriptor ShutdownRequested = new("Shutdown requested. Press Ctrl+C again to force exit.", "🛑", MessageSeverity.Output, s_id++);
        public static readonly MessageDescriptor ApplyUpdate_Error = new("{0}", "❌", MessageSeverity.Error, s_id++);
        public static readonly MessageDescriptor ApplyUpdate_Warning = new("{0}", "⚠", MessageSeverity.Warning, s_id++);
        public static readonly MessageDescriptor ApplyUpdate_Verbose = new("{0}", "⌚", MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ApplyUpdate_ChangingEntryPoint = new("{0} Press \"Ctrl + R\" to restart.", "⚠", MessageSeverity.Warning, s_id++);
        public static readonly MessageDescriptor ApplyUpdate_FileContentDoesNotMatchBuiltSource = new("{0} Expected if a source file is updated that is linked to project whose build is not up-to-date.", "⌚", MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ConfiguredToLaunchBrowser = new("dotnet-watch is configured to launch a browser on ASP.NET Core application startup.", "⌚", MessageSeverity.Verbose, s_id++);
        public static readonly MessageDescriptor ConfiguredToUseBrowserRefresh = new("Configuring the app to use browser-refresh middleware", "⌚", MessageSeverity.Verbose, s_id++);
    }

    internal interface IReporter
    {
        void Report(MessageDescriptor descriptor, string prefix, object?[] args);
        void ProcessOutput(string projectPath, string data);

        public bool IsVerbose
            => false;

        /// <summary>
        /// True to call <see cref="ProcessOutput"/> when launched process writes to standard output.
        /// Used for testing.
        /// </summary>
        bool ReportProcessOutput { get; }

        void Report(MessageDescriptor descriptor, params object?[] args)
            => Report(descriptor, prefix: "", args);

        void Verbose(string message, string emoji = "⌚")
            => Report(new MessageDescriptor(message, emoji, MessageSeverity.Verbose, Id: null));

        void Output(string message, string emoji = "⌚")
            => Report(new MessageDescriptor(message, emoji, MessageSeverity.Output, Id: null));

        void Warn(string message, string emoji = "⌚")
            => Report(new MessageDescriptor(message, emoji, MessageSeverity.Warning, Id: null));

        void Error(string message, string emoji = "❌")
            => Report(new MessageDescriptor(message, emoji, MessageSeverity.Error, Id: null));
    }
}
