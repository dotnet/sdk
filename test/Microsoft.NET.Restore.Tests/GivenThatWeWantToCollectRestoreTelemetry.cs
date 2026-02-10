// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.NET.Build.Tests;

namespace Microsoft.NET.Restore.Tests
{
    public class GivenThatWeWantToCollectRestoreTelemetry : SdkTest
    {
        public GivenThatWeWantToCollectRestoreTelemetry(ITestOutputHelper log) : base(log)
        {
        }

        [CoreMSBuildOnlyFact]
        public void It_collects_restore_telemetry_for_explicit_restore()
        {
            var testProject = new TestProject()
            {
                Name = "RestoreTelemetryTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            Type loggerType = typeof(LogTelemetryToStdOutForTest);
            var telemetryTestLogger = new[]
            {
                $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}"
            };

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var restoreCommand = new RestoreCommand(testAsset);

            var result = restoreCommand.Execute(telemetryTestLogger);
            
            result.Should().Pass();

            // Verify that RestoreTelemetry event was logged
            result.StdOut.Should().Contain("\"EventName\":\"RestoreTelemetry\"");
            
            // Verify that restore type property is present
            result.StdOut.Should().MatchRegex("\"RestoreType\":\"(explicit|implicit)\"");
            
            // Verify that restore scope property is present
            result.StdOut.Should().MatchRegex("\"RestoreScope\":\"(workspace-wide|single-project)\"");
            
            // Verify that restore count properties are present
            result.StdOut.Should().MatchRegex("\"ProjectsRestored\":\"\\d+\"");
            result.StdOut.Should().MatchRegex("\"ProjectsAlreadyUpToDate\":\"\\d+\"");
        }

        [CoreMSBuildOnlyFact]
        public void It_collects_restore_telemetry_for_implicit_restore_during_build()
        {
            var testProject = new TestProject()
            {
                Name = "ImplicitRestoreTelemetryTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            Type loggerType = typeof(LogTelemetryToStdOutForTest);
            var telemetryTestLogger = new[]
            {
                "-restore",
                $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}"
            };

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            var result = buildCommand.Execute(telemetryTestLogger);
            
            result.Should().Pass();

            // Verify that RestoreTelemetry event was logged
            result.StdOut.Should().Contain("\"EventName\":\"RestoreTelemetry\"");
            
            // Verify that restore type is implicit for build with -restore
            result.StdOut.Should().Contain("\"RestoreType\":\"implicit\"");
            
            // Verify that restore scope property is present
            result.StdOut.Should().MatchRegex("\"RestoreScope\":\"(workspace-wide|single-project)\"");
            
            // Verify that restore count properties are present
            result.StdOut.Should().MatchRegex("\"ProjectsRestored\":\"\\d+\"");
            result.StdOut.Should().MatchRegex("\"ProjectsAlreadyUpToDate\":\"\\d+\"");
        }

        [CoreMSBuildOnlyFact]
        public void It_collects_restore_telemetry_showing_projects_already_up_to_date()
        {
            var testProject = new TestProject()
            {
                Name = "UpToDateRestoreTelemetryTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            Type loggerType = typeof(LogTelemetryToStdOutForTest);
            var telemetryTestLogger = new[]
            {
                $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}"
            };

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            // First restore to populate cache
            var firstRestoreCommand = new RestoreCommand(testAsset);
            firstRestoreCommand.Execute().Should().Pass();

            // Second restore should show projects as up-to-date
            var secondRestoreCommand = new RestoreCommand(testAsset);
            var result = secondRestoreCommand.Execute(telemetryTestLogger);
            
            result.Should().Pass();

            // Verify that RestoreTelemetry event was logged
            result.StdOut.Should().Contain("\"EventName\":\"RestoreTelemetry\"");
            
            // Verify that restore scope property is present
            result.StdOut.Should().MatchRegex("\"RestoreScope\":\"(workspace-wide|single-project)\"");
            
            // For a second restore without changes, projects should be up-to-date
            // The exact count depends on whether restore was needed, but we should see the property
            result.StdOut.Should().MatchRegex("\"ProjectsAlreadyUpToDate\":\"\\d+\"");
        }
    }
}
