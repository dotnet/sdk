// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildWithGlobalJson : SdkTest
    {
        public GivenThatWeWantToBuildWithGlobalJson(ITestOutputHelper log) : base(log)
        { }

        [FullMSBuildOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_fails_build_on_failed_sdk_resolution(bool runningInVS)
        {
            var prevIncludeDefault = Environment.GetEnvironmentVariable("MSBUILDINCLUDEDEFAULTSDKRESOLVER");
            try
            {
                Environment.SetEnvironmentVariable("MSBUILDINCLUDEDEFAULTSDKRESOLVER", "false");
                TestProject testProject = new()
                {
                    Name = "FailedResolution",
                    TargetFrameworks = "net5.0"
                };

                var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: runningInVS.ToString());
                var globalJsonPath = Path.Combine(testAsset.Path, testProject.Name, "global.json");
                File.WriteAllText(globalJsonPath, @"{
    ""sdk"": {
    ""version"": ""9.9.999""
    }
    }");

                var buildCommand = new BuildCommand(testAsset);
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
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDINCLUDEDEFAULTSDKRESOLVER", prevIncludeDefault);
            }
        }
    }
}
