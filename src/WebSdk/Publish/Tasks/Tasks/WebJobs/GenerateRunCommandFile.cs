// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Sdk.Publish.Tasks.WebJobs
{
    public class GenerateRunCommandFile : Task
    {
        private const string RunCommandFile = "run";
        [Required]
        public string? ProjectDirectory { get; set; }
        [Required]
        public string? WebJobsDirectory { get; set; }
        [Required]
        public string? TargetPath { get; set; }
        [Required]
        public bool UseAppHost { get; set; }
        public string? ExecutableExtension { get; set; }
        public bool IsLinux { get; set; }

        public override bool Execute()
        {
            string runCmdFileExtension = IsLinux ? "sh" : "cmd";
            string runCmdFileName = $"{RunCommandFile}.{runCmdFileExtension}";

            bool isRunCommandFilePresent = ProjectDirectory is not null && File.Exists(Path.Combine(ProjectDirectory, runCmdFileName));
            if (!isRunCommandFilePresent)
            {
                string command = WebJobsCommandGenerator.RunCommand(TargetPath, UseAppHost, ExecutableExtension, IsLinux);
                if (WebJobsDirectory is not null)
                {
                    File.WriteAllText(Path.Combine(WebJobsDirectory, runCmdFileName), command);
                }
            }

            return true;
        }
    }
}
