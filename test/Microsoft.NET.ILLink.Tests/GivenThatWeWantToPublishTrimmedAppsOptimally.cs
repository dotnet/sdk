// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Newtonsoft.Json.Linq;
using static Microsoft.NET.ILLink.Tests.ILLinkTestUtils;

namespace Microsoft.NET.ILLink.Tests;

[TestClass]
public class GivenThatWeWantToPublishTrimmedAppsOptimally : SdkTest
{
    [TestMethod]
    [DataRow("")]
    [DataRow("true")]
    [DataRow("false")]
    public void Trimmed_publish_produces_a_runnable_deployment_without_redundant_root_output(string optimized)
    {
        var project = CreateProject();
        if (optimized != "")
        {
            project.AdditionalProperties["UseOptimizedPublish"] = optimized;
        }

        var asset = TestAssetsManager.CreateTestProject(project, identifier: optimized)
            .WithProjectChanges(AddBuildHook);
        var publish = new PublishCommand(asset);
        var result = publish.Execute();
        result.Should().Pass();
        if (optimized == "false")
        {
            result.Should().HaveStdOutContaining("Root Build executed");
        }
        else
        {
            result.Should().NotHaveStdOutContaining("Root Build executed");
        }

        var output = publish.GetOutputDirectory(runtimeIdentifier: project.RuntimeIdentifier).FullName;
        AssertDeployment(output);
        var buildOutput = new BuildCommand(asset).GetOutputDirectory(runtimeIdentifier: project.RuntimeIdentifier).FullName;
        if (optimized == "false")
        {
            File.Exists(Path.Combine(buildOutput, "TrimmedApp.dll")).Should().BeTrue();
            File.Exists(Path.Combine(buildOutput, "TrimmedApp.runtimeconfig.json")).Should().BeTrue();
        }
        else
        {
            Directory.GetFiles(buildOutput).Should().BeEmpty();
        }

        var libraryOutput = Path.Combine(asset.TestRoot, "Greeter", "bin", "Debug", ToolsetInfo.CurrentTargetFramework);
        File.Exists(Path.Combine(libraryOutput, "Greeter.dll")).Should().BeTrue("project references still run normal Build");
        DoesImageHaveMethod(Path.Combine(libraryOutput, "Greeter.dll"), "UnusedMethod").Should().BeTrue();
    }

    [TestMethod]
    public void Repeated_publish_updates_runtime_configuration_and_tracks_intermediate_outputs()
    {
        var project = CreateProject();
        project.AdditionalProperties["UserRuntimeConfig"] = "$(MSBuildProjectDirectory)/custom.runtimeconfig.json";
        project.SourceFiles["custom.runtimeconfig.json"] = """{"configProperties":{"Test.Setting":"first"}}""";
        var asset = TestAssetsManager.CreateTestProject(project);
        var publish = new PublishCommand(asset);
        publish.Execute("/p:ServerGarbageCollection=false").Should().Pass();
        var output = publish.GetOutputDirectory(runtimeIdentifier: project.RuntimeIdentifier).FullName;
        var intermediate = publish.GetIntermediateDirectory(runtimeIdentifier: project.RuntimeIdentifier).FullName;
        var runtimeConfig = Path.Combine(intermediate, "publish", "TrimmedApp.runtimeconfig.json");
        var firstWrite = File.GetLastWriteTimeUtc(runtimeConfig);

        publish.Execute("/p:ServerGarbageCollection=false").Should().Pass();
        File.GetLastWriteTimeUtc(runtimeConfig).Should().Be(firstWrite);
        AssertDeployment(output);

        publish.Execute("/p:ServerGarbageCollection=true").Should().Pass();
        ReadServerGC(Path.Combine(output, "TrimmedApp.runtimeconfig.json")).Should().BeTrue();
        ReadServerGC(runtimeConfig).Should().BeTrue();
        File.WriteAllText(Path.Combine(asset.TestRoot, "TrimmedApp", "custom.runtimeconfig.json"),
            """{"configProperties":{"Test.Setting":"second"}}""");
        publish.Execute("/p:ServerGarbageCollection=true").Should().Pass();
        JObject.Parse(File.ReadAllText(Path.Combine(output, "TrimmedApp.runtimeconfig.json")))
            ["runtimeOptions"]!["configProperties"]!["Test.Setting"]!.Value<string>().Should().Be("second");
        var writes = File.ReadAllText(Path.Combine(intermediate, "TrimmedApp.csproj.FileListAbsolute.txt"));
        writes.Should().Contain(runtimeConfig);

        new BuildCommand(asset).Execute().Should().Pass();
        File.Exists(runtimeConfig).Should().BeTrue();
        AssertDeployment(output);
        publish.Execute("/p:ServerGarbageCollection=true").Should().Pass();
        AssertDeployment(output);

        new CleanCommand(asset).Execute().Should().Pass();
        File.Exists(runtimeConfig).Should().BeFalse("Clean should remove the tracked intermediate runtime configuration");
    }

    [TestMethod]
    [DataRow("Build;Publish", false)]
    [DataRow("Publish;Build", false)]
    [DataRow("Build;Publish", true)]
    [DataRow("Publish;Build", true)]
    public void Explicit_build_and_publish_keep_distinct_runtime_configuration_paths(string targets, bool previouslyBuilt)
    {
        var project = CreateProject();
        project.AdditionalProperties["ProjectRuntimeConfigFilePath"] = "$(MSBuildProjectDirectory)/custom-build/renamed.runtimeconfig.json";
        project.AdditionalProperties["ProjectRuntimeConfigDevFilePath"] = "$(MSBuildProjectDirectory)/custom-build/renamed.runtimeconfig.dev.json";
        project.AdditionalProperties["ProjectRuntimeConfigFileName"] = "TrimmedApp.runtimeconfig.json";
        project.AdditionalProperties["IntermediateOutputPath"] = "custom-obj/";
        project.AdditionalProperties["PublishDir"] = "$(MSBuildProjectDirectory)/custom-publish/";
        project.AdditionalProperties["PublishDepsFilePath"] = "$(MSBuildProjectDirectory)/custom-publish/TrimmedApp.deps.json";
        var asset = TestAssetsManager.CreateTestProject(project, identifier: targets.Replace(';', '_') + previouslyBuilt)
            .WithProjectChanges(AddBuildHook);
        var root = Path.Combine(asset.TestRoot, "TrimmedApp");
        Directory.CreateDirectory(Path.Combine(root, "custom-build"));
        Directory.CreateDirectory(Path.Combine(root, "custom-publish"));

        new RestoreCommand(asset).Execute().Should().Pass();
        if (previouslyBuilt)
        {
            new BuildCommand(asset).Execute().Should().Pass();
        }
        new MSBuildCommand(asset, targets).Execute().Should().Pass()
            .And.HaveStdOutContaining("Root Build executed");

        var buildConfig = Path.Combine(root, "custom-build", "renamed.runtimeconfig.json");
        var intermediate = Path.Combine(root, "custom-obj", project.TargetFrameworks, project.RuntimeIdentifier!);
        var publishConfig = Path.Combine(intermediate, "publish", "TrimmedApp.runtimeconfig.json");
        File.Exists(buildConfig).Should().BeTrue();
        File.Exists(Path.Combine(root, "custom-build", "renamed.runtimeconfig.dev.json")).Should().BeTrue();
        File.Exists(publishConfig).Should().BeTrue();
        File.ReadAllText(buildConfig).Should().Be(File.ReadAllText(publishConfig));
        AssertDeployment(Path.Combine(root, "custom-publish"));
        File.Exists(Path.Combine(new BuildCommand(asset).GetOutputDirectory(runtimeIdentifier: project.RuntimeIdentifier).FullName, "TrimmedApp.dll"))
            .Should().BeTrue("an explicitly requested Build must still deploy its outputs");
        File.ReadAllText(Path.Combine(intermediate, "TrimmedApp.csproj.FileListAbsolute.txt")).Should().Contain(publishConfig);
        new BuildCommand(asset).Execute().Should().Pass();
        File.Exists(publishConfig).Should().BeTrue("a subsequent Build must not remove the reusable publish configuration");
        new CleanCommand(asset).Execute().Should().Pass();
        File.Exists(publishConfig).Should().BeFalse();
    }

    [TestMethod]
    public void Build_then_no_build_publish_uses_build_metadata_without_compiling()
    {
        var project = CreateProject();
        var asset = TestAssetsManager.CreateTestProject(project);
        new BuildCommand(asset).Execute("/p:ServerGarbageCollection=false").Should().Pass();
        asset.WithProjectChanges(xml => xml.Root!.Add(XElement.Parse("""
            <Target Name="FailIfCompiled" BeforeTargets="CoreCompile">
              <Error Text="NoBuild must not compile" />
            </Target>
            """)));

        var publish = new PublishCommand(asset);
        publish.Execute("/p:NoBuild=true", "/p:ServerGarbageCollection=true").Should().Pass();
        var output = publish.GetOutputDirectory(runtimeIdentifier: project.RuntimeIdentifier).FullName;
        AssertDeployment(output);
        ReadServerGC(Path.Combine(output, "TrimmedApp.runtimeconfig.json")).Should().BeFalse();
        File.Exists(Path.Combine(publish.GetIntermediateDirectory(runtimeIdentifier: project.RuntimeIdentifier).FullName,
            "publish", "TrimmedApp.runtimeconfig.json")).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("PublishTrimmed", "false")]
    [DataRow("PublishSingleFile", "true")]
    [DataRow("PublishReadyToRun", "true")]
    [DataRow("SelfContained", "false")]
    [DataRow("TargetFramework", "net10.0")]
    [DataRow("TargetPlatformIdentifier", "windows")]
    [DataRow("RuntimeIdentifier", "browser-wasm")]
    [DataRow("RuntimeIdentifier", "android-arm64")]
    [DataRow("RuntimeIdentifier", "ios-arm64")]
    [DataRow("RuntimeIdentifier", "maccatalyst-arm64")]
    [DataRow("UsingMicrosoftNETSdkWeb", "true")]
    [DataRow("UsingMicrosoftNETSdkRazor", "true")]
    [DataRow("UsingMicrosoftNETSdkWorker", "true")]
    [DataRow("UsingMicrosoftNETSdkWebAssembly", "true")]
    [DataRow("UseMaui", "true")]
    [DataRow("UseWPF", "true")]
    [DataRow("UseWindowsForms", "true")]
    [DataRow("EnableComHosting", "true")]
    [DataRow("UseIJWHost", "true")]
    [DataRow("EnableDynamicLoading", "true")]
    [DataRow("RegisterForComInterop", "true")]
    [DataRow("GenerateSerializationAssemblies", "On")]
    [DataRow("PublishProtocol", "ClickOnce")]
    [DataRow("GenerateClickOnceManifests", "true")]
    [DataRow("PackAsTool", "true")]
    public void Explicit_true_cannot_enable_unsupported_trimmed_scenarios(string property, string value)
    {
        var project = CreateProject();
        var asset = TestAssetsManager.CreateTestProject(project, identifier: property + value);
        var command = new GetValuesCommand(asset, "_UseOptimizedPublish",
            targetFramework: property == "TargetFramework" ? value : project.TargetFrameworks)
        {
            ShouldCompile = false,
            ShouldRestore = false
        };
        command.Execute($"/p:{property}={value}").Should().Pass();
        command.GetValues().Should().NotContain("true");
        command.Execute("/p:UseOptimizedPublish=true", $"/p:{property}={value}").Should().Pass();
        command.GetValues().Should().NotContain("true");
    }

    [TestMethod]
    [DataRow("PublishSingleFile")]
    [DataRow("PublishReadyToRun")]
    [DataRow("EnableComHosting")]
    [DataRow("UseMaui")]
    public void Existing_aot_eligibility_is_not_narrowed(string property)
    {
        var project = CreateProject();
        var asset = TestAssetsManager.CreateTestProject(project, identifier: property);
        var command = new GetValuesCommand(asset, "_UseOptimizedPublish") { ShouldCompile = false, ShouldRestore = false };
        command.Execute("/p:PublishAot=true", $"/p:{property}=true").Should().Pass();
        command.GetValues().Should().Equal("true");
    }

    private static TestProject CreateProject()
    {
        var rid = EnvironmentInfo.GetCompatibleRid(ToolsetInfo.CurrentTargetFramework);
        if (rid is not ("win-x64" or "win-arm64" or "linux-x64" or "linux-arm64" or
                        "linux-musl-x64" or "linux-musl-arm64" or "osx-x64" or "osx-arm64"))
        {
            Assert.Inconclusive($"Optimized trimmed publish is not enabled for the test host RID '{rid}'.");
        }

        var library = new TestProject { Name = "Greeter", TargetFrameworks = ToolsetInfo.CurrentTargetFramework };
        library.SourceFiles["Greeter.cs"] = """
            public static class Greeter
            {
                public static void Greet() => System.Console.WriteLine("Hello from reference");
                public static void UnusedMethod() { }
            }
            """;
        var project = new TestProject
        {
            Name = "TrimmedApp",
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            IsExe = true,
            RuntimeIdentifier = rid,
            SelfContained = "true"
        };
        project.ReferencedProjects.Add(library);
        project.AdditionalProperties["PublishTrimmed"] = "true";
        project.AdditionalProperties["GenerateDocumentationFile"] = "true";
        project.AdditionalProperties["InvariantGlobalization"] = "false";
        project.SourceFiles["Program.cs"] = """
            using System;
            using System.Globalization;
            using System.IO;
            using System.Resources;
            Greeter.Greet();
            Console.WriteLine(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content.txt")));
            Console.WriteLine(new ResourceManager("TrimmedApp.Messages", typeof(Program).Assembly)
                .GetString("Greeting", CultureInfo.GetCultureInfo("fr")));
            """;
        project.SourceFiles["content.txt"] = "Hello from content";
        project.SourceFiles["app.config"] = "<configuration />";
        project.SourceFiles["Messages.resx"] = Resource("Hello");
        project.SourceFiles["Messages.fr.resx"] = Resource("Bonjour");
        project.AdditionalItems.Add(new("Content", new()
        {
            ["Include"] = "content.txt",
            ["CopyToOutputDirectory"] = "PreserveNewest"
        }));
        return project;
    }

    private static string Resource(string greeting) => $"""
        <root>
          <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
          <resheader name="version"><value>2.0</value></resheader>
          <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms</value></resheader>
          <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms</value></resheader>
          <data name="Greeting" xml:space="preserve"><value>{greeting}</value></data>
        </root>
        """;

    private static void AddBuildHook(XDocument project) => project.Root!.Add(XElement.Parse("""
        <Target Name="ConfirmRootBuild" AfterTargets="Build" Condition="'$(MSBuildProjectName)' == 'TrimmedApp'">
          <Message Importance="High" Text="Root Build executed" />
        </Target>
        """));

    private void AssertDeployment(string output)
    {
        File.Exists(Path.Combine(output, "TrimmedApp.runtimeconfig.json")).Should().BeTrue();
        File.Exists(Path.Combine(output, "TrimmedApp.xml")).Should().BeTrue();
        File.Exists(Path.Combine(output, "TrimmedApp.dll.config")).Should().BeTrue();
        File.Exists(Path.Combine(output, "fr", "TrimmedApp.resources.dll")).Should().BeTrue();
        DoesDepsFileHaveAssembly(Path.Combine(output, "TrimmedApp.deps.json"), "Greeter").Should().BeTrue();
        DoesImageHaveMethod(Path.Combine(output, "Greeter.dll"), "UnusedMethod").Should().BeFalse();
        new RunExeCommand(Log, Path.Combine(output, $"TrimmedApp{Constants.ExeSuffix}"))
            .Execute().Should().Pass()
            .And.HaveStdOutContaining("Hello from reference")
            .And.HaveStdOutContaining("Hello from content")
            .And.HaveStdOutContaining("Bonjour");
    }

    private static bool ReadServerGC(string path) =>
        JObject.Parse(File.ReadAllText(path))["runtimeOptions"]!["configProperties"]!["System.GC.Server"]!.Value<bool>();
}
