// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.NET.Build.Tasks
{
    public class GetTestsProject : Microsoft.Build.Utilities.Task
    {
        [Required]
        public ITaskItem TargetPath { get; set; }

        [Required]
        public ITaskItem GetTestsProjectPipeName { get; set; }

        [Required]
        public ITaskItem ProjectFullPath { get; set; }

        [Required]
        public ITaskItem TargetFramework { get; set; }

        public ITaskItem RunSettingsFilePath { get; set; } = new TaskItem(string.Empty);

        public ITaskItem IsTestingPlatformApplication { get; set; } = new TaskItem(string.Empty);

        public override bool Execute()
        {
            try
            {
                Log.LogMessage(MessageImportance.Low, $"Target path: {TargetPath}");

                NamedPipeClient dotnetTestPipeClient = new(GetTestsProjectPipeName.ItemSpec);

                dotnetTestPipeClient.RegisterSerializer(new ModuleMessageSerializer(), typeof(ModuleMessage));
                dotnetTestPipeClient.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));

                dotnetTestPipeClient.ConnectAsync(CancellationToken.None).GetAwaiter().GetResult();
                dotnetTestPipeClient.RequestReplyAsync<ModuleMessage, VoidResponse>(new ModuleMessage(TargetPath.ItemSpec, ProjectFullPath.ItemSpec, TargetFramework.ItemSpec, RunSettingsFilePath.ItemSpec, IsTestingPlatformApplication.ItemSpec), CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);

            }
            return !Log.HasLoggedErrors;
        }
    }
}
