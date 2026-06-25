// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.NET.Build.Tests
{
    [TestClass]
    public class GivenThatAProjectHasntBeenRestored : SdkTest
    {

        [TestMethod]
        [DataRow("TestLibrary", null)]
        [DataRow("TestApp", null)]
        [DataRow("TestApp", "netcoreapp2.1")]
        [DataRow("TestApp", ToolsetInfo.CurrentTargetFramework)]
        public void The_build_fails_if_nuget_restore_has_not_occurred(string relativeProjectPath, string targetFramework)
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("AppWithLibrary", identifier: relativeProjectPath + "_" + targetFramework ?? string.Empty)
                .WithSource()
                .WithTargetFramework(targetFramework, relativeProjectPath);

            var projectDirectory = Path.Combine(testAsset.TestRoot, relativeProjectPath);

            VerifyNotRestoredFailure(projectDirectory);
        }

        private void VerifyNotRestoredFailure(string projectDirectory)
        {
            var buildCommand = new BuildCommand(Log, projectDirectory);

            var assetsFile = Path.Combine(buildCommand.GetBaseIntermediateDirectory().FullName, "project.assets.json");

            buildCommand
                //  Pass "/clp:summary" so that we can check output for string "1 Error(s)"
                .ExecuteWithoutRestore("/clp:summary")
                .Should()
                .Fail()
                .And.HaveStdOutContaining(assetsFile)
                //  We should only get one error
                .And.HaveStdOutContaining("1 Error(s)");
        }

        [TestMethod]
        public void ReadingCacheDoesNotFail()
        {
            var testProject = new TestProject()
            {
                Name = "App",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            var result = buildCommand
                .Execute();

            result
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("NETSDK1062");
        }
    }
}
