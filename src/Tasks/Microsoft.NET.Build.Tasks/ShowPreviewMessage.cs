// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Provides a localizable mechanism for logging messages with different levels of importance from the SDK targets.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public class ShowPreviewMessage : TaskBase, IMultiThreadableTask
    {
#if NETFRAMEWORK
        private TaskEnvironment _taskEnvironment;
        public TaskEnvironment TaskEnvironment
        {
            get => _taskEnvironment ??= new TaskEnvironment(new ProcessTaskEnvironmentDriver(Directory.GetCurrentDirectory()));
            set => _taskEnvironment = value;
        }
#else
        public TaskEnvironment TaskEnvironment { get; set; } = null!;
#endif

        protected override void ExecuteCore()
        {
            const string previewMessageKey = "Microsoft.NET.Build.Tasks.DisplayPreviewMessageKey";

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
