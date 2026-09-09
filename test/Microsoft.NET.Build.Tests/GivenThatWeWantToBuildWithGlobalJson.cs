// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    [TestClass]
    public class GivenThatWeWantToBuildWithGlobalJson : SdkTest
    {

        [TestMethod]
        [FullMSBuildOnly]
        // MSBUILDINCLUDEDEFAULTSDKRESOLVER is process-global and is inherited by the child MSBuild
        // processes that other test classes spawn concurrently under class-level parallelization,
        // so hold the environment-variable lock for the duration of the test.
        [ResourceLock(WellKnownResources.EnvironmentVariables)]
        [DataRow(true)]
        [DataRow(false)]
        public void It_fails_build_on_failed_sdk_resolution(bool runningInVS)
        {
            TestProject testProject = new()
            {
                Name = "FailedResolution",
                TargetFrameworks = "net5.0"
            };

            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: runningInVS.ToString());
            var globalJsonPath = Path.Combine(testAsset.Path, testProject.Name, "global.json");
            File.WriteAllText(globalJsonPath, @"{
    ""sdk"": {
    ""version"": ""9.9.999""
    }
    }");

            // Scope MSBUILDINCLUDEDEFAULTSDKRESOLVER to the build we launch instead of setting it
            // process-wide: the process-global variable is inherited by every MSBuild process any
            // concurrently running test starts, which breaks SDK resolution for those tests
            // (MSTEST0074).
            var buildCommand = new BuildCommand(testAsset)
                .WithEnvironmentVariable("MSBUILDINCLUDEDEFAULTSDKRESOLVER", "false");
            var result = buildCommand.Execute($"/p:BuildingInsideVisualStudio={runningInVS}", $"/bl:binlog{runningInVS}.binlog")
                .Should()
                .Fail();
            var warningString = "warning : Unable to locate the .NET SDK";
            var errorString = "Unable to locate the .NET SDK. Check that it is installed";
            if (runningInVS)
            {
                result.And
                    .HaveStdOutContaining(warningString)
                    .And
                    .NotHaveStdOutContaining(errorString)
                    .And
                    .HaveStdOutContaining("NETSDK1141");
            }
            else
            {
                result.And
                    .HaveStdOutContaining(errorString)
                    .And
                    .NotHaveStdOutContaining(warningString);
            }
        }
    }
}
