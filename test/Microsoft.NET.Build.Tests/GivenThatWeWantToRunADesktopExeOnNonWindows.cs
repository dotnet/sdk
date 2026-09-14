// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    [TestClass]
    public class GivenThatWeWantToRunADesktopExeOnNonWindows : SdkTest
    {
        [TestMethod]
        [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
        public void It_errors_when_running_a_NETFramework_exe_on_non_Windows()
        {
            var testProject = new TestProject
            {
                TargetFrameworks = "net472",
                IsExe = true,
            };

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            new DotnetCommand(Log, "run")
            {
                WorkingDirectory = Path.Combine(testAsset.TestRoot, testProject.Name!)
            }
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1243");
        }
    }
}
