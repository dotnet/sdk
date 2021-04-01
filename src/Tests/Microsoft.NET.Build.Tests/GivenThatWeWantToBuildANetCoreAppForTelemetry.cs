using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using System.Reflection;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildANetCoreAppAndPassingALogger : SdkTest
    {
        public GivenThatWeWantToBuildANetCoreAppAndPassingALogger(ITestOutputHelper log) : base(log)
        {
        }

        [CoreMSBuildOnlyFact]
        public void It_collects_single_TargetFramework_moniker_and_other_properties()
        {
            string targetFramework = "netcoreapp1.0";
            var testProject = new TestProject()
            {
                Name = "TelemetryTest-SingleTarget",
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
                .Contain("{\"EventName\":\"TargetFrameworkEval\",\"Properties\":{\"TargetFrameworkMoniker\":\".NETCoreApp,Version=v1.0\",\"RuntimeIdentifier\":\"null\",\"SelfContained\":\"null\",\"UseApphost\":\"null\",\"OutputType\":\"Library\"}");
        }

        [CoreMSBuildOnlyFact]
        public void It_collects_multiple_TargetFramework_monikers_and_other_properties()
        {
            string targetFrameworks = "net46;netcoreapp1.1";
            var testProject = new TestProject()
            {
                Name = "TelemetryTest-MultipleTargets",
                TargetFrameworks = targetFrameworks,
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
                .Contain(
                    "{\"EventName\":\"TargetFrameworkEval\",\"Properties\":{\"TargetFrameworkMoniker\":\".NETFramework,Version=v4.6\",\"RuntimeIdentifier\":\"null\",\"SelfContained\":\"null\",\"UseApphost\":\"null\",\"OutputType\":\"Library\"}")
                .And
                .Contain(
                    "{\"EventName\":\"TargetFrameworkEval\",\"Properties\":{\"TargetFrameworkMoniker\":\".NETCoreApp,Version=v1.1\",\"RuntimeIdentifier\":\"null\",\"SelfContained\":\"null\",\"UseApphost\":\"null\",\"OutputType\":\"Library\"}");
        }
    }
}
