// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenACheckForUnsupportedWinMDReferencesMultiThreading
    {
        [Fact]
        public void RelativeManagedReferencePath_ResolvesFromTaskEnvironment()
        {
            using var tte = new TaskTestEnvironment();

            const string managedReference = "refs/managed.dll";
            const string winmdReference = "refs/fake.winmd";

            tte.CreateProjectDirectory("refs");
            File.Copy(typeof(CheckForUnsupportedWinMDReferences).Assembly.Location, tte.GetProjectPath(managedReference));
            tte.CreateProjectFile(winmdReference, string.Empty);

            File.Exists(managedReference).Should().BeFalse("the process CWD is not the project directory");

            var task = new CheckForUnsupportedWinMDReferences
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tte.TaskEnvironment,
                TargetFrameworkMoniker = ".NETCoreApp,Version=v5.0",
                ReferencePaths = new ITaskItem[]
                {
                    new MockTaskItem(managedReference, new Dictionary<string, string>()),
                    new MockTaskItem(winmdReference, new Dictionary<string, string>()),
                },
            };

            task.Execute().Should().BeFalse("the .winmd reference should still log an unsupported reference error");

            var engine = (MockBuildEngine)task.BuildEngine;
            engine.Errors.Should().ContainSingle();
        }
    }
}
