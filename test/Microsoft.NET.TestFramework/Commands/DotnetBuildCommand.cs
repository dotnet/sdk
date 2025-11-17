// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.NET.TestFramework.Commands
{
    public class DotnetBuildCommand : DotnetCommand
    {
        public TestAsset? TestAsset { get; }

        public DotnetBuildCommand(ITestOutputHelper log, params string[] args) : base(log)
        {
            Arguments.Add("build");
            Arguments.AddRange(args);
        }

        public DotnetBuildCommand(TestAsset testAsset, params string[] args) : this(testAsset.Log, args)
        {
            TestAsset = testAsset;

            if (testAsset.TestProject != null && testAsset.TestProject.Name is not null)
            {
                WorkingDirectory = Path.Combine(testAsset.TestRoot, testAsset.TestProject.Name);
            }
            else
            {
                WorkingDirectory = testAsset.TestRoot;
            }
        }

        public DirectoryInfo GetOutputDirectory(string? targetFramework = null, string configuration = "Debug", string? runtimeIdentifier = null, string? platform = null)
        {
            Debug.Assert(TestAsset?.TestProject?.Name != null);
            var projectPath = Path.Combine(TestAsset.Path, TestAsset.TestProject.Name);
            return new DirectoryInfo(OutputPathCalculator.FromProject(projectPath, TestAsset)
                .GetOutputDirectory(targetFramework, configuration, runtimeIdentifier, platform));
        }
    }
}
