﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Configuration;
using Xunit.Abstractions;
using System;

namespace Microsoft.NET.TestFramework.Commands
{
    public class NuGetExeRestoreCommand : TestCommand
    {
        private readonly string _projectRootPath;
        public string ProjectRootPath => _projectRootPath;

        public string ProjectFile { get; }

        public string NuGetExeVersion { get; set; }

        public string FullPathProjectFile => Path.Combine(ProjectRootPath, ProjectFile);

        public NuGetExeRestoreCommand(ITestOutputHelper log, string projectRootPath, string relativePathToProject = null) : base(log)
        {
            _projectRootPath = projectRootPath;
            ProjectFile = MSBuildCommand.FindProjectFile(ref _projectRootPath, relativePathToProject);
        }

        protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            var newArgs = new List<string>();

            newArgs.Add("restore");

            newArgs.Add(FullPathProjectFile);

            newArgs.Add("-PackagesDirectory");
            newArgs.Add(TestContext.Current.NuGetCachePath);

            newArgs.AddRange(args);

            if (string.IsNullOrEmpty(TestContext.Current.NuGetExePath))
            {
                throw new InvalidOperationException("Path to nuget.exe not set");
            }

            var nugetExePath = TestContext.Current.NuGetExePath;
            if (!string.IsNullOrEmpty(NuGetExeVersion))
            {
                nugetExePath = Path.Combine(Path.GetDirectoryName(nugetExePath), NuGetExeVersion, "nuget.exe");
            }

            if (!File.Exists(nugetExePath))
            {
                string directory = Path.GetDirectoryName(nugetExePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string url = string.IsNullOrEmpty(NuGetExeVersion) ?
                    "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" :
                    $"https://dist.nuget.org/win-x86-commandline/v{NuGetExeVersion}/nuget.exe";
                var client = new System.Net.WebClient();
                client.DownloadFile(url, nugetExePath);
            }

            var ret = new SdkCommandSpec()
            {
                FileName = nugetExePath,
                Arguments = newArgs
            };

            TestContext.Current.AddTestEnvironmentVariables(ret);

            return ret;
        }
    }
}
