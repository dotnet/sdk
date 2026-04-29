// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Provides a localizable mechanism for logging messages with different levels of importance from the SDK targets.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public class ShowPreviewMessage : TaskBase, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        private static readonly object s_previewMessageLock = new();

        protected override void ExecuteCore()
        {
            const string previewMessageKey = "Microsoft.NET.Build.Tasks.DisplayPreviewMessageKey";

            lock (s_previewMessageLock)
            {
                object messageDisplayed =
                    BuildEngine4.GetRegisteredTaskObject(previewMessageKey, RegisteredTaskObjectLifetime.Build);
                if (messageDisplayed == null)
                {
                    Log.LogMessage(MessageImportance.High, Strings.UsingPreviewSdk);

                    BuildEngine4.RegisterTaskObject(
                        previewMessageKey,
                        new object(),
                        RegisteredTaskObjectLifetime.Build,
                        true);
                }
            }
        }
    }
}
