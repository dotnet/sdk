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

        private string CreateTargetFrameworkEvalTelemetryJson(
            string targetFrameworkVersion,
            string targetPlatformIdentifier = "null",
            string runtimeIdentifier = "null",
            string selfContained = "null",
            string useApphost = "null",
            string outputType = "Library",
            string useArtifactsOutput = "null",
            string artifactsPathLocationType = "null",
            string useMonoRuntime = "null",
            string publishAot = "null",
            string publishTrimmed = "null",
            string publishSelfContained = "null",
            string publishReadyToRun = "null",
            string publishReadyToRunComposite = "false",
            string publishProtocol = "null",
            string configuration = "Debug")
        {
            return $"{{\"EventName\":\"targetframeworkeval\",\"Properties\":{{\"TargetFrameworkVersion\":\"{targetFrameworkVersion}\",\"RuntimeIdentifier\":\"{runtimeIdentifier}\",\"SelfContained\":\"{selfContained}\",\"UseApphost\":\"{useApphost}\",\"OutputType\":\"{outputType}\",\"UseArtifactsOutput\":\"{useArtifactsOutput}\",\"ArtifactsPathLocationType\":\"{artifactsPathLocationType}\",\"TargetPlatformIdentifier\":\"{targetPlatformIdentifier}\",\"UseMonoRuntime\":\"{useMonoRuntime}\",\"PublishAot\":\"{publishAot}\",\"PublishTrimmed\":\"{publishTrimmed}\",\"PublishSelfContained\":\"{publishSelfContained}\",\"PublishReadyToRun\":\"{publishReadyToRun}\",\"PublishReadyToRunComposite\":\"{publishReadyToRunComposite}\",\"PublishProtocol\":\"{publishProtocol}\",\"Configuration\":\"{configuration}\"}}";
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

            buildCommand
                .Execute(TelemetryTestLogger)
                .StdOut.Should()
                .Contain(CreateTargetFrameworkEvalTelemetryJson(
                    $".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}"));
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

            result
                .StdOut.Should()
                .Contain(CreateTargetFrameworkEvalTelemetryJson(
                    ".NETFramework,Version=v4.6", 
                    targetPlatformIdentifier: "Windows",
                    publishReadyToRunComposite: "null"))
                .And
                .Contain(CreateTargetFrameworkEvalTelemetryJson(
                    $".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}"));
        }
    }
}
