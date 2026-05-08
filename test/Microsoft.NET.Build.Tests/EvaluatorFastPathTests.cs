// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class EvaluatorFastPathTests : SdkTest
    {
        public EvaluatorFastPathTests(ITestOutputHelper log) : base(log)
        {

        }

        [Fact]
        public void FastPathDoesNotNeedReflection()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("MSBuildBareBonesProject")
                .WithSource();
            var command = new MSBuildCommand(testAsset, string.Empty);
            command
                .WithEnvironmentVariable("MSBuildLogPropertyFunctionsRequiringReflection", "true")
                .WithWorkingDirectory(testAsset.Path);
            command
                .ExecuteWithoutRestore()
                .Should()
                .Pass();

            var logPath = Path.Combine(testAsset.Path, "PropertyFunctionsRequiringReflection");
            File.Exists(logPath).Should().BeFalse();
        }

        [Theory]
        [InlineData("console")]
        [InlineData("webapp")]
        public void EnsureDotnetCommonProjectPropertyFunctionsOnFastPath(string alias)
        {
            var testDir = _testAssetsManager.CreateTestDirectory().Path;

            new DotnetNewCommand(Log, alias)
                .WithoutCustomHive()
                .WithWorkingDirectory(testDir)
                .Execute()
                .Should()
                .Pass();

            new DotnetBuildCommand(Log)
                .WithWorkingDirectory(testDir)
                .WithEnvironmentVariable("MSBuildLogPropertyFunctionsRequiringReflection", "true")
                .Execute()
                .Should()
                .Pass();

            var logPath = Path.Combine(testDir, "PropertyFunctionsRequiringReflection");
            // Functions from Microsoft.Build.Utilities.ToolLocationHelper are pending to add fast path, tracked by https://github.com/dotnet/msbuild/issues/10411.
            // This verification should be changed to no log file created once it is done.
            var toolLocationHelper_GetPlatformSDKLocation = "ReceiverType=Microsoft.Build.Utilities.ToolLocationHelper; ObjectInstanceType=; MethodName=GetPlatformSDKLocation(String, String)";
            var toolLocationHelper_GetPlatformSDKDisplayName = "ReceiverType=Microsoft.Build.Utilities.ToolLocationHelper; ObjectInstanceType=; MethodName=GetPlatformSDKDisplayName(String, String)";
            var lines = File.ReadAllLines(logPath);
            var allOnFastPathWithExceptions = lines.All(l => (toolLocationHelper_GetPlatformSDKLocation.Equals(l) || toolLocationHelper_GetPlatformSDKDisplayName.Equals(l)));
            allOnFastPathWithExceptions.Should().BeTrue("If this test fails, file a bug to add the new fast path in MSBuild like https://github.com/dotnet/msbuild/issues/12029. You may add an exclusion with a comment in this test while that is in process.");
        }
    }
}
