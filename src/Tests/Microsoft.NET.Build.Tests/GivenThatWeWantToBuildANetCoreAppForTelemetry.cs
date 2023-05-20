// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildANetCoreAppAndPassingALogger : SdkTest
    {
        public GivenThatWeWantToBuildANetCoreAppAndPassingALogger(ITestOutputHelper log) : base(log)
        {
        }

        [CoreMSBuildOnlyFact]
        public void It_collects_TargetFramework_version_and_other_properties()
        {
            string targetFramework = ToolsetInfo.CurrentTargetFramework;
            var testProject = new TestProject()
            {
                Name = "FrameworkTargetTelemetryTest",
                TargetFrameworks = targetFramework,
            };
            Type loggerType = typeof(LogTelemetryToStdOutForTest);
            var TelemetryTestLogger = new[]
                {
                    $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}"
                };
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            var tfmHashValue = Sha256Hasher.HashWithNormalizedCasing($".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}");
            buildCommand
                .Execute(TelemetryTestLogger)
                .StdOut.Should()
                .Contain($"{{\"EventName\":\"targetframeworkeval\",\"Properties\":{{\"TargetFrameworkVersion\":\"{tfmHashValue}\",\"RuntimeIdentifier\":\"fb329000228cc5a24c264c57139de8bf854fc86fc18bf1c04ab61a2b5cb4b921\",\"SelfContained\":\"fb329000228cc5a24c264c57139de8bf854fc86fc18bf1c04ab61a2b5cb4b921\",\"UseAppHost\":\"fb329000228cc5a24c264c57139de8bf854fc86fc18bf1c04ab61a2b5cb4b921\",\"OutputType\":\"Library\",\"UseArtifactsOutput\":\"fb329000228cc5a24c264c57139de8bf854fc86fc18bf1c04ab61a2b5cb4b921\",\"ArtifactsPathLocationType\":\"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855\"}}");
        }

        [CoreMSBuildOnlyFact]
        public void It_collects_multi_TargetFramework_version_and_other_properties()
        {
            string targetFramework = $"net46;{ToolsetInfo.CurrentTargetFramework}";

            var testProject = new TestProject()
            {
                Name = "MultitargetTelemetry",
                TargetFrameworks = targetFramework,
            };
            Type loggerType = typeof(LogTelemetryToStdOutForTest);
            var TelemetryTestLogger = new[]
                {
                    $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}"
                };
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            var result = buildCommand
                .Execute(TelemetryTestLogger);

            var netTfmHashValue = Sha256Hasher.HashWithNormalizedCasing($".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}");
            var net46TfmHashValue = Sha256Hasher.HashWithNormalizedCasing(".NETFramework,Version=v4.6");
            result
                .StdOut.Should()
                .Contain(
                    $"{{\"EventName\":\"targetframeworkeval\",\"Properties\":{{\"TargetFrameworkVersion\":\"{net46TfmHashValue}\",\"RuntimeIdentifier\":\"fb329000228cc5a24c264c57139de8bf854fc86fc18bf1c04ab61a2b5cb4b921\",\"SelfContained\":\"fb329000228cc5a24c264c57139de8bf854fc86fc18bf1c04ab61a2b5cb4b921\",\"UseAppHost\":\"fb329000228cc5a24c264c57139de8bf854fc86fc18bf1c04ab61a2b5cb4b921\",\"OutputType\":\"Library\",\"UseArtifactsOutput\":\"fb329000228cc5a24c264c57139de8bf854fc86fc18bf1c04ab61a2b5cb4b921\",\"ArtifactsPathLocationType\":\"null\"}}")
                .And
                .Contain(
                    $"{{\"EventName\":\"targetframeworkeval\",\"Properties\":{{\"TargetFrameworkVersion\":\"{netTfmHashValue}\",\"RuntimeIdentifier\":\"fb329000228cc5a24c264c57139de8bf854fc86fc18bf1c04ab61a2b5cb4b921\",\"SelfContained\":\"fb329000228cc5a24c264c57139de8bf854fc86fc18bf1c04ab61a2b5cb4b921\",\"UseAppHost\":\"fb329000228cc5a24c264c57139de8bf854fc86fc18bf1c04ab61a2b5cb4b921\",\"OutputType\":\"Library\",\"UseArtifactsOutput\":\"fb329000228cc5a24c264c57139de8bf854fc86fc18bf1c04ab61a2b5cb4b921\",\"ArtifactsPathLocationType\":\"null\"}}");
        }
    }
}
