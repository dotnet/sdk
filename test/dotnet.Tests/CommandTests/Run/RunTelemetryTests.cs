// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Run.Tests;

public class RunTelemetryTests : SdkTest
{
    public RunTelemetryTests(ITestOutputHelper log) : base(log)
    {
    }

    [Fact]
    public void GetFileBasedIdentifier_ReturnsDifferentHashesForDifferentPaths()
    {
        // Arrange
        var path1 = "/some/path/to/file1.cs";
        var path2 = "/some/path/to/file2.cs";

        // Act
        var hash1 = RunTelemetry.GetFileBasedIdentifier(path1);
        var hash2 = RunTelemetry.GetFileBasedIdentifier(path2);

        // Assert
        hash1.Should().NotBe(hash2);
        hash1.Should().HaveLength(64); // SHA256 hex string length
        hash2.Should().HaveLength(64);
    }

    [Fact]
    public void GetProjectBasedIdentifier_ReturnsSameHashForSamePath()
    {
        // Arrange
        var path = "/some/path/to/project.csproj";

        // Act
        var hash1 = RunTelemetry.GetProjectBasedIdentifier(path);
        var hash2 = RunTelemetry.GetProjectBasedIdentifier(path);

        // Assert
        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64);
    }

    [Fact]
    public void GetProjectBasedIdentifier_UsesRelativePathWhenRepoRootProvided()
    {
        // Arrange
        var repoRoot = "/repo/root".Replace('/', Path.DirectorySeparatorChar);
        var projectPath = "/repo/root/src/project.csproj".Replace('/', Path.DirectorySeparatorChar);
        var expectedRelativePath = "src/project.csproj".Replace('/', Path.DirectorySeparatorChar);

        // Act
        var hashWithRepo = RunTelemetry.GetProjectBasedIdentifier(projectPath, repoRoot);
        var hashOfRelative = RunTelemetry.GetProjectBasedIdentifier(expectedRelativePath);

        // Assert
        hashWithRepo.Should().Be(hashOfRelative);
    }

    [Fact]
    public void CountSdks_FileBasedApp_CountsDirectives()
    {
        // Arrange
        var directives = ImmutableArray.Create<CSharpDirective>(
            new CSharpDirective.Sdk(default) { Name = "Microsoft.NET.Sdk" },
            new CSharpDirective.Sdk(default) { Name = "Microsoft.NET.Sdk.Web", Version = "1.0.0" }
        );

        // Act
        var count = RunTelemetry.CountSdks(directives);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public void CountSdks_FileBasedApp_NoDirectives_ReturnsDefaultOne()
    {
        // Arrange
        var directives = ImmutableArray<CSharpDirective>.Empty;

        // Act
        var count = RunTelemetry.CountSdks(directives);

        // Assert
        count.Should().Be(1); // Default Microsoft.NET.Sdk
    }

    [Fact]
    public void CountPackageReferences_FileBasedApp_CountsDirectives()
    {
        // Arrange
        var directives = ImmutableArray.Create<CSharpDirective>(
            new CSharpDirective.Package(default) { Name = "Newtonsoft.Json", Version = "13.0.1" },
            new CSharpDirective.Package(default) { Name = "Microsoft.Extensions.DependencyInjection" }
        );

        // Act
        var count = RunTelemetry.CountPackageReferences(directives);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public void CountProjectReferences_FileBasedApp_CountsDirectives()
    {
        // Arrange
        var directives = ImmutableArray.Create<CSharpDirective>(
            new CSharpDirective.Project(default) { Name = "../lib/Library.csproj" },
            new CSharpDirective.Project(default) { Name = "../common/Common.csproj" }
        );

        // Act
        var count = RunTelemetry.CountProjectReferences(directives);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public void CountAdditionalProperties_CountsPropertyDirectives()
    {
        // Arrange
        var directives = ImmutableArray.Create<CSharpDirective>(
            new CSharpDirective.Property(default) { Name = "TargetFramework", Value = "net8.0" },
            new CSharpDirective.Property(default) { Name = "Nullable", Value = "enable" }
        );

        // Act
        var count = RunTelemetry.CountAdditionalProperties(directives);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public void TrackRunEvent_FileBasedApp_SendsCorrectTelemetry()
    {
        // Arrange
        var events = new List<(string? eventName, IDictionary<string, string?>? properties, IDictionary<string, double>? measurements)>();
        
        void handler(object? sender, InstrumentationEventArgs args) => events.Add((args.EventName, args.Properties, args.Measurements));
        
        TelemetryEventEntry.EntryPosted += handler;

        try
        {
            // Act
            RunTelemetry.TrackRunEvent(
                isFileBased: true,
                projectIdentifier: "test-hash",
                launchProfile: "(Default)",
                noLaunchProfile: false,
                launchSettings: null,
                sdkCount: 2,
                packageReferenceCount: 3,
                projectReferenceCount: 1,
                additionalPropertiesCount: 2,
                usedMSBuild: true,
                usedRoslynCompiler: false);

            // Assert
            events.Should().HaveCount(1);
            var eventData = events[0];
            eventData.eventName.Should().Be("run");
            eventData.properties.Should().NotBeNull();
            
            var props = eventData.properties!;
            props["app_type"].Should().Be("file_based");
            props["project_id"].Should().Be("test-hash");
            props["sdk_count"].Should().Be("2");
            props["package_reference_count"].Should().Be("3");
            props["project_reference_count"].Should().Be("1");
            props["additional_properties_count"].Should().Be("2");
            props["used_msbuild"].Should().Be("true");
            props["used_roslyn_compiler"].Should().Be("false");
            props["launch_profile_requested"].Should().Be("explicit");
            props["launch_profile_is_default"].Should().Be("true");
        }
        finally
        {
            // Cleanup
            TelemetryEventEntry.EntryPosted -= handler;
        }
    }

    [Fact]
    public void TrackRunEvent_ProjectBasedApp_SendsCorrectTelemetry()
    {
        // Arrange
        var events = new List<(string? eventName, IDictionary<string, string?>? properties, IDictionary<string, double>? measurements)>();
        
        void handler(object? sender, InstrumentationEventArgs args) => events.Add((args.EventName, args.Properties, args.Measurements));
        
        TelemetryEventEntry.EntryPosted += handler;

        try
        {
            // Act
            RunTelemetry.TrackRunEvent(
                isFileBased: false,
                projectIdentifier: "project-hash",
                launchProfile: null,
                noLaunchProfile: true,
                launchSettings: null,
                sdkCount: 1,
                packageReferenceCount: 5,
                projectReferenceCount: 2);

            // Assert
            events.Should().HaveCount(1);
            var eventData = events[0];
            eventData.eventName.Should().Be("run");
            eventData.properties.Should().NotBeNull();
            
            var props = eventData.properties!;
            props["app_type"].Should().Be("project_based");
            props["project_id"].Should().Be("project-hash");
            props["sdk_count"].Should().Be("1");
            props["package_reference_count"].Should().Be("5");
            props["project_reference_count"].Should().Be("2");
            props["launch_profile_requested"].Should().Be("none");
            props.Should().NotContainKey("additional_properties_count");
            props.Should().NotContainKey("used_msbuild");
            props.Should().NotContainKey("used_roslyn_compiler");
        }
        finally
        {
            // Cleanup
            TelemetryEventEntry.EntryPosted -= handler;
        }
    }

    [Fact]
    public void TrackRunEvent_WithDefaultLaunchProfile_MarksTelemetryCorrectly()
    {
        // Arrange
        var events = new List<(string? eventName, IDictionary<string, string?>? properties, IDictionary<string, double>? measurements)>();
        
        void handler(object? sender, InstrumentationEventArgs args) => events.Add((args.EventName, args.Properties, args.Measurements));
        
        TelemetryEventEntry.EntryPosted += handler;

        var launchSettings = new ProjectLaunchSettingsModel
        {
            LaunchProfileName = "(Default)"
        };

        try
        {
            // Act
            RunTelemetry.TrackRunEvent(
                isFileBased: false,
                projectIdentifier: "test-hash",
                launchProfile: null,
                noLaunchProfile: false,
                launchSettings: launchSettings,
                sdkCount: 1,
                packageReferenceCount: 0,
                projectReferenceCount: 0);

            // Assert
            events.Should().HaveCount(1);
            var props = events[0].properties!;
            props["launch_profile_requested"].Should().Be("default_used");
            props["launch_profile_is_default"].Should().Be("true");
        }
        finally
        {
            // Cleanup
            TelemetryEventEntry.EntryPosted -= handler;
        }
    }
}
