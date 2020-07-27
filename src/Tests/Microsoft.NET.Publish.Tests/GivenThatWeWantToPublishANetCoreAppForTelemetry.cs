﻿using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.NET.Build.Tests;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishANetCoreAppForTelemetry : SdkTest
    {
        public GivenThatWeWantToPublishANetCoreAppForTelemetry(ITestOutputHelper log) : base(log)
        {
        }

        [CoreMSBuildOnlyTheory]
        [InlineData("net5.0")]
        public void It_collects_empty_Trimmer_SingleFile_ReadyToRun_publishing_properties(string targetFramework)
        {
            Type loggerType = typeof(LogTelemetryToStdOutForTest);
            var TelemetryTestLogger = new[]
                {
                    $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}"
                };

            var testProject = CreateTestProject(targetFramework, "PlainProject");
            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(Log, Path.Combine(testProjectInstance.Path, testProject.Name));
            publishCommand.Execute(TelemetryTestLogger).StdOut.Should().Contain(
                "{\"EventName\":\"PublishProperties\",\"Properties\":{\"PublishReadyToRun\":\"null\",\"PublishTrimmed\":\"null\",\"PublishSingleFile\":\"null\"}");
        }

        [CoreMSBuildOnlyTheory]
        [InlineData("net5.0")]
        public void It_collects_Trimmer_SingleFile_ReadyToRun_publishing_properties(string targetFramework)
        {
            Type loggerType = typeof(LogTelemetryToStdOutForTest);
            var TelemetryTestLogger = new[]
                {
                    $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}"
                };

            var testProject = CreateTestProject(targetFramework, "TrimmedR2RSingleFileProject", true, true, true);
            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(Log, Path.Combine(testProjectInstance.Path, testProject.Name));
            string s = publishCommand.Execute(TelemetryTestLogger).StdOut;//.Should()
            s.Should().Contain(
                "{\"EventName\":\"PublishProperties\",\"Properties\":{\"PublishReadyToRun\":\"True\",\"PublishTrimmed\":\"True\",\"PublishSingleFile\":\"True\"}");
            s.Should().Contain(
                "{\"EventName\":\"ReadyToRun\",\"Properties\":{\"PublishReadyToRunUseCrossgen2\":\"null\",\"Crossgen2PackVersion\":\"null\"");
            s.Should().Contain(
                "\"FailedCount\":\"0\"");
            s.Should().MatchRegex(
                "\"CompileListCount\":\"[1-9]\\d?\"");  // Do not hardcode number of assemblies being compiled here, due to ILTrimmer
        }

        [CoreMSBuildOnlyTheory]
        [InlineData("net5.0")] 
        void It_collects_crossgen2_publishing_properties(string targetFramework)
        {
            // Crossgen2 only supported for Linux/Windows x64 scenarios for now
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.OSArchitecture != Architecture.X64)
                return;

            Type loggerType = typeof(LogTelemetryToStdOutForTest);
            var TelemetryTestLogger = new[]
                {
                    $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}"
                };

            var testProject = CreateTestProject(targetFramework, "TrimmedR2RSingleFileProject", r2r: true);
            testProject.AdditionalProperties["PublishReadyToRunUseCrossgen2"] = "True";

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(Log, Path.Combine(testProjectInstance.Path, testProject.Name));
            publishCommand.Execute(TelemetryTestLogger).StdOut.Should()
                .Contain(
                    "{\"EventName\":\"PublishProperties\",\"Properties\":{\"PublishReadyToRun\":\"True\",\"PublishTrimmed\":\"null\",\"PublishSingleFile\":\"null\"}")
                .And.Contain(
                    "{\"EventName\":\"ReadyToRun\",\"Properties\":{\"PublishReadyToRunUseCrossgen2\":\"True\",")
                .And.MatchRegex(
                    "\"Crossgen2PackVersion\":\"5.+\"")
                .And.Contain(
                    "\"CompileListCount\":\"1\",\"FailedCount\":\"0\"");
        }

        private TestProject CreateTestProject(string targetFramework, string projectName, bool trimmer = false, bool r2r = false, bool singleFile = false)
        {
            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = targetFramework,
                IsExe = true,
                IsSdkProject = true,
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework)
            };
            if (r2r)
            {
                testProject.AdditionalProperties["PublishReadyToRun"] = "True";
            }
            if (trimmer)
            {
                testProject.AdditionalProperties["PublishTrimmed"] = "True";
            }
            if (singleFile)
            {
                testProject.AdditionalProperties["PublishSingleFile"] = "True";
            }

            testProject.SourceFiles[$"{projectName}.cs"] = @"
using System;
public class Program
{
    public static void Main()
    {
        Console.WriteLine(""Hello world"");
    }
}";

            return testProject;
        }
    }
}
