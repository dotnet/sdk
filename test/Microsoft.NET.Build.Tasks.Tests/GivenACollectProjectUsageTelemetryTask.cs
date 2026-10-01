// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks.UnitTests;

[TestClass]
public class GivenACollectProjectUsageTelemetryTask
{
    private static readonly string ProjectDirectory = Path.Combine(Path.GetTempPath(), "usage", "App");
    private static readonly string ProjectPath = Path.Combine(ProjectDirectory, "App.CSPROJ");

    private static CollectProjectUsageTelemetry CreateTask(CapturingBuildEngine engine) => new()
    {
        BuildEngine = engine,
        ProjectPath = ProjectPath,
        TargetFramework = "net10.0",
        RuntimeIdentifier = "win-x64",
        RuntimeIdentifiers = "linux-x64; win-x64;;",
        SdkVersion = "10.0.100",
    };

    private static ITaskItem Item(string spec, string? metadataName = null, string? metadataValue = null)
    {
        var item = new TaskItem(spec);
        if (metadataName is not null)
        {
            item.SetMetadata(metadataName, metadataValue);
        }

        return item;
    }

    [TestMethod]
    public void ItLogsProjectShape()
    {
        var engine = new CapturingBuildEngine();

        CreateTask(engine).Execute().Should().BeTrue();

        engine.EventName.Should().Be("projectusage");
        IDictionary<string, string> properties = engine.Properties!;
        properties["ProjectId"].Should().HaveLength(64);
        properties["ProjectType"].Should().Be(".csproj");
        properties["TargetFramework"].Should().Be("net10.0");
        properties["RuntimeIdentifier"].Should().Be("win-x64");
        properties["RuntimeIdentifiers"].Should().Be("linux-x64;win-x64");
        properties["SdkVersion"].Should().Be("10.0.100");
    }

    [TestMethod]
    public void FrameworkAndPackageReferencesAreClearTextAndOthersAreHashed()
    {
        var engine = new CapturingBuildEngine();
        CollectProjectUsageTelemetry task = CreateTask(engine);
        task.References =
        [
            Item(Path.Combine("packs", "System.Runtime.dll"), "FrameworkReferenceName", "Microsoft.NETCore.App"),
            Item(Path.Combine("nuget", "Newtonsoft.Json.dll"), "NuGetPackageId", "Newtonsoft.Json"),
            Item(Path.Combine("bin", "Contoso.Secret.dll")),
        ];

        task.Execute().Should().BeTrue();

        string[] references = engine.Properties!["References"].Split(';');
        references.Should().HaveCount(3);
        references.Should().Contain("System.Runtime");
        references.Should().Contain("Newtonsoft.Json");
        references.Should().Contain(CollectProjectUsageTelemetry.Hash("CONTOSO.SECRET", null));
    }

    private sealed class CapturingBuildEngine : MockBuildEngine, IBuildEngine5
    {
        public string? EventName { get; private set; }

        public IDictionary<string, string>? Properties { get; private set; }

        public void LogTelemetry(string eventName, IDictionary<string, string> properties)
        {
            EventName = eventName;
            Properties = properties;
        }
    }
}
