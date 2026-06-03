// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToRunADesktopExeOnNonWindows : SdkTest
    {
        public GivenThatWeWantToRunADesktopExeOnNonWindows(ITestOutputHelper log) : base(log)
        {
        }

        [PlatformSpecificFact(TestPlatforms.Linux | TestPlatforms.OSX | TestPlatforms.FreeBSD)]
        public void It_errors_with_NETSDK1241_when_running_a_NETFramework_exe_on_non_Windows()
        {
            var testProject = new TestProject
            {
                TargetFrameworks = "net472",
                IsExe = true,
            };

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            new DotnetCommand(Log, "run")
            {
                WorkingDirectory = Path.Combine(testAsset.TestRoot, testProject.Name)
            }
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1241");
        }
    }
}
