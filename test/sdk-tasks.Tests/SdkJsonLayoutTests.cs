// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security;
using System.Text.Json.Nodes;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests;

// In-process MSBuild changes process-wide state that other layout tests use.
[DoNotParallelize]
public class SdkJsonLayoutTests : SdkTest
{
    private const string Targets = "_PublishRoslynFiles;PublishDotnetFormat;PublishTestCli;GenerateCliRuntimeConfigurationFiles;" +
        "MakeFscRunnableAndMoveToPublishDir;RemoveFscFilesAfterPublish;RetargetTools;RemoveResourcesFromDotnetDeps";

    private const string RuntimeConfig = """
        {
          "runtimeOptions": {
            "frameworks": [
              { "name": "Microsoft.NETCore.App", "version": "1.0.0" },
              { "name": "Microsoft.AspNetCore.App", "version": "2.0.0" },
              { "name": "Microsoft.WindowsDesktop.App", "version": "3.0.0" }
            ],
            "rollForward": "Major",
            "configProperties": { "Custom.Property": "preserved" }
          },
          "custom": "preserved"
        }
        """;

    private const string Deps = """
        {
          "runtimeTarget": { "name": "test/test-rid" },
          "targets": {
            "test": {
              "redist/1.0.0": {
                "runtime": { "redist.dll": {}, "tool_msbuild.dll": {}, "tool_nuget.dll": {}, "tool_cli.dll": {} },
                "resources": { "en/redist.resources.dll": { "locale": "en" } }
              },
              "Dependency/2.0.0": {
                "runtime": { "Helper.dll": { "custom": "preserved" } },
                "runtimeTargets": {
                  "runtimes/any/native/extension.targets": { "assetType": "native", "rid": "any" },
                  "runtimes/any/native/extension.props": { "assetType": "native", "rid": "any" },
                  "runtimes/test/native/keep.so": { "assetType": "native", "rid": "test" }
                },
                "resources": { "en/Helper.resources.dll": { "locale": "en" } }
              }
            }
          },
          "libraries": {
            "redist/1.0.0": { "type": "project", "custom": "preserved" },
            "Dependency/2.0.0": { "type": "package", "sha512": "preserved" }
          },
          "custom": "preserved"
        }
        """;

    /// <summary>
    /// Verifies synthetic runtime assets and relocated imports are removed while dependency
    /// libraries and unrelated fields survive, and only dotnet loses resource sections.
    /// </summary>
    [TestMethod]
    public void FinalizationPreservesDependenciesAndToolSpecificResources()
    {
        JsonProject project = CreateProject();
        project.Build();

        foreach (string tool in new[] { "MSBuild", "dotnet", "NuGet.CommandLine.XPlat" })
        {
            JsonNode deps = ReadJson(project.Output(tool + ".deps.json"));
            JsonNode? libraries = deps["libraries"];
            Assert.IsNotNull(libraries);
            libraries.AsObject().Select(pair => pair.Key).Should().Equal("Dependency/2.0.0", tool + ".deps.json/1.0.0");
            deps["custom"]?.GetValue<string>().Should().Be("preserved");
            JsonNode? targets = deps["targets"];
            Assert.IsNotNull(targets);
            foreach (JsonNode? target in targets.AsObject().Select(pair => pair.Value))
            {
                Assert.IsNotNull(target);
                JsonNode? runtime = target[tool + ".deps.json/1.0.0"]?["runtime"];
                Assert.IsNotNull(runtime);
                runtime.AsObject().Should().BeEmpty();
                target["Dependency/2.0.0"]?["runtime"]?["Helper.dll"]?["custom"]?.GetValue<string>().Should().Be("preserved");
                JsonNode? runtimeTargets = target["Dependency/2.0.0"]?["runtimeTargets"];
                Assert.IsNotNull(runtimeTargets);
                runtimeTargets.AsObject().Select(pair => pair.Key)
                    .Should().Equal("runtimes/test/native/keep.so");
                foreach (JsonNode? library in target.AsObject().Select(pair => pair.Value))
                {
                    Assert.IsNotNull(library);
                    (library["resources"] != null).Should().Be(tool != "dotnet");
                }
            }
        }
        File.ReadAllText(project.Output("extension.targets")).Should().Be("targets");
        File.ReadAllText(project.Output("extension.props")).Should().Be("props");
        foreach (string name in new[] { "tool_msbuild", "tool_nuget", "tool_cli", "redist" })
        {
            File.Exists(project.Output(name + ".dll")).Should().BeFalse();
            File.Exists(project.Output(name + ".pdb")).Should().BeFalse();
        }
    }

    /// <summary>
    /// Verifies deps edits and reverts produce the same JSON
    /// as clean finalization and stabilize on subsequent unchanged invocations.
    /// </summary>
    [TestMethod]
    public void DepsEditsAndRevertsMatchCleanFinalization()
    {
        JsonProject project = CreateProject();
        project.Build();
        Dictionary<string, byte[]> original = Contents(project.JsonFiles);
        project.EditDepsInput(changed: true);
        project.Build();
        project.JsonFiles.Any(path => !File.ReadAllBytes(path).SequenceEqual(original[path])).Should().BeTrue();

        JsonProject clean = CreateProject();
        clean.EditDepsInput(changed: true);
        clean.Build();
        foreach (string path in project.JsonFiles)
        {
            File.ReadAllBytes(path).Should().Equal(File.ReadAllBytes(clean.Output(Path.GetRelativePath(project.OutputDirectory, path))));
        }
        Dictionary<string, DateTime> changed = Snapshot(project.JsonFiles);
        project.Build();
        AssertTimestamps(changed);

        project.EditDepsInput(changed: false);
        project.Build();
        AssertContents(original);
    }

    /// <summary>
    /// Verifies each deleted finalized or package-copied JSON output is repaired without
    /// rewriting its settled siblings or depending on installer-wide directory deletion.
    /// </summary>
    [TestMethod]
    public void DeletedOutputsAreRepairedWithoutRewritingSiblings()
    {
        JsonProject project = CreateProject();
        project.Build();
        foreach (string missing in project.JsonFiles.Where(path => path != project.Output("redist.deps.json") &&
            path != project.Output("redist.runtimeconfig.json")))
        {
            byte[] expected = File.ReadAllBytes(missing);
            Dictionary<string, DateTime> siblings = Snapshot(project.JsonFiles.Where(path => path != missing));
            File.Delete(missing);
            project.Build();
            File.ReadAllBytes(missing).Should().Equal(expected);
            AssertTimestamps(siblings);
        }
    }

    /// <summary>
    /// Verifies F# retains unmodified synthetic publish inputs outside the SDK, removes
    /// stale shipping tool_fsc files, and leaves unrelated F# payload files intact.
    /// </summary>
    [TestMethod]
    public void FSharpRetainsRawInputsWithoutShippingSyntheticFiles()
    {
        JsonProject project = CreateProject();
        project.Build();
        byte[] raw = File.ReadAllBytes(project.FSharpInput("tool_fsc.deps.json"));
        File.ReadAllBytes(project.FSharpPublished("tool_fsc.deps.json")).Should().Equal(raw);
        File.ReadAllText(project.FSharpPublished("tool_fsc.deps.json")).Should().Contain("tool_fsc.dll");
        File.ReadAllText(project.Output("FSharp", "fsc.deps.json")).Should().NotContain("tool_fsc.dll");
        File.ReadAllBytes(project.Output("FSharp", "fsc.deps.json")).Should().Equal(File.ReadAllBytes(project.Output("FSharp", "fsi.deps.json")));
        File.ReadAllBytes(project.Output("FSharp", "fsc.runtimeconfig.json")).Should().Equal(File.ReadAllBytes(project.Output("FSharp", "fsi.runtimeconfig.json")));
        foreach (string suffix in new[] { ".dll", ".pdb", ".deps.json", ".runtimeconfig.json" })
        {
            Write(project.Output("FSharp", "tool_fsc" + suffix), "stale");
        }
        string unowned = Write(project.Output("FSharp", "unowned.txt"), "preserved");
        project.Build();
        foreach (string suffix in new[] { ".dll", ".pdb", ".deps.json", ".runtimeconfig.json" })
        {
            File.Exists(project.Output("FSharp", "tool_fsc" + suffix)).Should().BeFalse();
        }
        File.ReadAllText(unowned).Should().Be("preserved");
        File.ReadAllText(project.Output("FSharp", "payload.dll")).Should().Be("payload");
    }

    private JsonProject CreateProject() =>
        new(TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString("N")).Path);

    private static JsonNode ReadJson(string path)
    {
        JsonNode? node = JsonNode.Parse(File.ReadAllText(path));
        Assert.IsNotNull(node);
        return node;
    }

    private static Dictionary<string, byte[]> Contents(IEnumerable<string> paths) =>
        paths.ToDictionary(path => path, File.ReadAllBytes);

    private static Dictionary<string, DateTime> Snapshot(IEnumerable<string> paths) =>
        paths.ToDictionary(path => path, File.GetLastWriteTimeUtc);

    private static void AssertContents(Dictionary<string, byte[]> contents)
    {
        foreach ((string path, byte[] bytes) in contents)
        {
            File.ReadAllBytes(path).Should().Equal(bytes, "{0} is unchanged", path);
        }
    }

    private static void AssertTimestamps(Dictionary<string, DateTime> timestamps)
    {
        foreach ((string path, DateTime timestamp) in timestamps)
        {
            File.GetLastWriteTimeUtc(path).Should().Be(timestamp, "{0} is unchanged", path);
        }
    }

    private static string Write(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)
            ?? throw new ArgumentException("The file must have a parent directory.", nameof(path)));
        File.WriteAllText(path, contents);
        return path;
    }

    private static string Escape(string value) =>
        SecurityElement.Escape(value) ?? throw new ArgumentNullException(nameof(value));

    private sealed class JsonProject
    {
        private readonly string _project;
        private readonly string _originalDeps;

        public JsonProject(string root)
        {
            Root = root;
            PrepareOutput(OutputDirectory);
            _originalDeps = File.ReadAllText(Output("redist.deps.json"));
            string packages = Path.Combine(root, "packages");
            string roslyn = Path.Combine(packages, "microsoft.net.compilers.toolset", "1.0.0", "tasks", "netcore", "bincore");
            string testCli = Path.Combine(packages, "microsoft.testplatform.cli", "1.0.0", "contentFiles", "any", "net10.0");
            string format = Path.Combine(root, "bin", "dotnet-format", "Debug", "test-framework");
            Write(Path.Combine(roslyn, "csc.runtimeconfig.json"), RuntimeConfig);
            Write(Path.Combine(testCli, "vstest.console.runtimeconfig.json"), RuntimeConfig);
            Write(Path.Combine(format, "BuildHost-netcore", "BuildHost.runtimeconfig.json"), RuntimeConfig);
            Write(Path.Combine(testCli, "testhost-latest.runtimeconfig.json"), RuntimeConfig);
            Write(FSharpInput("tool_fsc.dll"), "synthetic");
            Write(FSharpInput("tool_fsc.pdb"), "symbols");
            Write(FSharpInput("payload.dll"), "payload");
            Write(FSharpInput("tool_fsc.runtimeconfig.json"), RuntimeConfig);
            Write(FSharpInput("tool_fsc.deps.json"), Deps.Replace("redist", "tool_fsc"));

            string assembly = typeof(UpdateRuntimeConfig).Assembly.Location;
            string builtIn = Path.Combine(Path.GetDirectoryName(typeof(Project).Assembly.Location)
                ?? throw new InvalidOperationException("The MSBuild assembly must have a parent directory."), "Microsoft.Build.Tasks.Core.dll");
            string usingTasks = string.Join(Environment.NewLine,
                new[] { "ChangeEntryPointLibraryName", "RemoveAssetFromDepsPackages", "UpdateRuntimeConfig" }
                    .Select(name => $"<UsingTask TaskName=\"{name}\" AssemblyFile=\"{Escape(assembly)}\" />")
                    .Concat(new[] { "Copy", "Delete", "Move", "RemoveDir", "MSBuild" }
                        .Select(name => $"<UsingTask TaskName=\"Microsoft.Build.Tasks.{name}\" AssemblyFile=\"{Escape(builtIn)}\" />")));
            string source = LayoutFiles.GetPath("GenerateLayout.targets",
                Path.Combine("src", "Layout", "redist", "targets", "GenerateLayout.targets"));
            File.Copy(source, Path.Combine(root, "GenerateLayout.targets"));
            _project = Write(Path.Combine(root, "json-layout.proj"), $$"""
                <Project>
                  {{usingTasks}}
                  <PropertyGroup>
                    <OutputPath>{{Escape(OutputDirectory + Path.DirectorySeparatorChar)}}</OutputPath>
                    <IntermediateOutputPath>{{Escape(Path.Combine(root, "obj") + Path.DirectorySeparatorChar)}}</IntermediateOutputPath>
                    <ArtifactsBinDir>{{Escape(Path.Combine(root, "bin") + Path.DirectorySeparatorChar)}}</ArtifactsBinDir>
                    <NuGetPackageRoot>{{Escape(packages + Path.DirectorySeparatorChar)}}</NuGetPackageRoot>
                    <Configuration>Debug</Configuration>
                    <NetCurrent>test-framework</NetCurrent>
                    <TargetName>redist</TargetName>
                    <OSName>win</OSName>
                    <MicrosoftNetCompilersToolsetVersion>1.0.0</MicrosoftNetCompilersToolsetVersion>
                    <MicrosoftTestPlatformCLIPackageVersion>1.0.0</MicrosoftTestPlatformCLIPackageVersion>
                    <MicrosoftNETCoreAppRuntimePackageVersion>9.0.0</MicrosoftNETCoreAppRuntimePackageVersion>
                    <MicrosoftAspNetCoreAppRuntimePackageVersion>8.0.0</MicrosoftAspNetCoreAppRuntimePackageVersion>
                  </PropertyGroup>
                  <Import Project="GenerateLayout.targets" />
                </Project>
                """);
            Write(Path.Combine(root, "tools", "tool_fsc.csproj"), $$"""
                <Project>
                  <UsingTask TaskName="Microsoft.Build.Tasks.Copy" AssemblyFile="{{Escape(builtIn)}}" />
                  <Target Name="Publish">
                    <ItemGroup>
                      <_Input Include="{{Escape(Path.Combine(root, "fsharp-input"))}}/**/*" />
                    </ItemGroup>
                    <Copy SourceFiles="@(_Input)"
                          DestinationFiles="@(_Input->'$(PublishDir)/%(RecursiveDir)%(Filename)%(Extension)')"
                          SkipUnchangedFiles="true" />
                  </Target>
                </Project>
                """);
        }

        public string Root { get; }
        public string OutputDirectory => Path.Combine(Root, "out");
        public string[] JsonFiles => Directory.GetFiles(OutputDirectory, "*.json", SearchOption.AllDirectories);
        public string Output(params string[] parts) => Path.Combine([OutputDirectory, .. parts]);
        public string FSharpInput(string name) => Path.Combine(Root, "fsharp-input", name);
        public string FSharpPublished(string name) => Path.Combine(Root, "obj", "fsharp-publish", name);

        public void PrepareOutput(string directory)
        {
            JsonNode? deps = JsonNode.Parse(Deps);
            Assert.IsNotNull(deps);
            JsonNode? targets = deps["targets"];
            Assert.IsNotNull(targets);
            JsonNode? target = targets["test"];
            Assert.IsNotNull(target);
            targets.AsObject().Add("test/test-rid", target.DeepClone());
            Write(Path.Combine(directory, "redist.deps.json"), deps.ToJsonString());
            Write(Path.Combine(directory, "redist.runtimeconfig.json"), RuntimeConfig);
            Write(Path.Combine(directory, "runtimes", "any", "native", "extension.targets"), "targets");
            Write(Path.Combine(directory, "runtimes", "any", "native", "extension.props"), "props");
            foreach (string name in new[] { "tool_msbuild", "tool_nuget", "tool_cli", "redist" })
            {
                Write(Path.Combine(directory, name + ".dll"), "synthetic");
                Write(Path.Combine(directory, name + ".pdb"), "symbols");
            }
        }

        public void EditDepsInput(bool changed) =>
            Write(Output("redist.deps.json"), changed ? _originalDeps.Replace("preserved", "edited") : _originalDeps);

        public void Build(Dictionary<string, string>? properties = null, bool success = true)
        {
            using var collection = new ProjectCollection(properties ?? []);
            var logger = new JsonLogger();
            SdkLayoutBuild.Run(collection, _project, Root, Targets.Split(';'), logger, () => logger.Text, success);
        }
    }

    private sealed class JsonLogger : Logger
    {
        private readonly List<string> _messages = [];
        public string Text => string.Join(Environment.NewLine, _messages);

        public override void Initialize(IEventSource source)
        {
            source.AnyEventRaised += (_, args) =>
            {
                if (args.Message is not null)
                {
                    _messages.Add(args.Message);
                }
            };
        }
    }
}
