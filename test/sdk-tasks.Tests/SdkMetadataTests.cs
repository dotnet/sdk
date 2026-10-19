// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security;
using System.Text;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests;

/// <summary>
/// Verifies metadata write stability, RID-chain invalidation, and recovery by invoking
/// the production SDK layout targets with isolated package inputs and output directories.
/// </summary>
[TestClass]
// In-process MSBuild saves/restores process-wide state shared with other layout tests.
[DoNotParallelize]
public class SdkMetadataTests : SdkTest
{
    private const string MetadataTargets = "GenerateBundledVersions;PublishVersionFile;CopyKnownWorkloadManifestFile";
    private const string ChainTarget = "GenerateSdkRuntimeIdentifierChain";
    private const string Graph = """
        {
          "runtimes": {
            "linux-x64": { "#import": [ "linux" ] },
            "linux": { "#import": [ "any" ] },
            "win-x64": { "#import": [ "win" ] },
            "win": { "#import": [ "any" ] },
            "any": {}
          }
        }
        """;

    /// <summary>
    /// Verifies scalar and item changes produce the same metadata as a clean build,
    /// repeated inputs preserve timestamps, and reverting inputs restores the original bytes.
    /// </summary>
    [TestMethod]
    [DataRow("PackageVersion", "11.0.200-test")]
    public void MetadataTracksPropertyAndItemEditsAndReverts(string property, string value)
    {
        MetadataProject project = CreateProject();
        project.Build(MetadataTargets);
        Dictionary<string, byte[]> original = project.MetadataFiles.ToDictionary(path => path, File.ReadAllBytes);

        project.Build(MetadataTargets, new() { [property] = value });
        project.MetadataFiles.Any(path => !File.ReadAllBytes(path).SequenceEqual(original[path])).Should().BeTrue();
        MetadataProject clean = CreateProject();
        clean.Build(MetadataTargets, new() { [property] = value });
        foreach (string path in project.MetadataFiles)
        {
            string cleanPath = Path.Combine(clean.Root, Path.GetRelativePath(project.Root, path));
            File.ReadAllBytes(path).Should().Equal(File.ReadAllBytes(cleanPath), "incremental metadata must match a clean build");
        }
        Dictionary<string, DateTime> changedTimestamps = AgeAndSnapshot(project.MetadataFiles);
        project.Build(MetadataTargets, new() { [property] = value });
        AssertTimestamps(changedTimestamps);

        project.Build(MetadataTargets);
        AssertContents(original);
    }

    /// <summary>
    /// Verifies RID-only edits and reverts regenerate the UTF-8, LF-separated chain,
    /// while warm builds skip the task and preserve chain and state-file timestamps.
    /// </summary>
    [TestMethod]
    public void RuntimeIdentifierChainSkipsWarmBuildsAndTracksRidChangesAndReverts()
    {
        MetadataProject project = CreateProject();
        project.Build(ChainTarget).Tasks.Should().Contain(ChainTarget);
        File.ReadAllBytes(project.ChainFile).Should().Equal(Encoding.UTF8.GetBytes("linux-x64\nlinux\nany"));
        Dictionary<string, DateTime> warm = Snapshot([project.ChainFile, .. project.StateFiles]);
        project.Build(ChainTarget).Tasks.Should().NotContain(ChainTarget);
        AssertTimestamps(warm);

        project.Build(ChainTarget, new() { ["PortableProductMonikerRid"] = "win-x64" }).Tasks.Should().Contain(ChainTarget);
        File.ReadAllText(project.ChainFile).Should().Be("win-x64\nwin\nany");
        project.Build(ChainTarget, new() { ["PortableProductMonikerRid"] = "win-x64" }).Tasks.Should().NotContain(ChainTarget);
        project.Build(ChainTarget).Tasks.Should().Contain(ChainTarget);
        File.ReadAllText(project.ChainFile).Should().Be("linux-x64\nlinux\nany");
        project.Build(ChainTarget).Tasks.Should().NotContain(ChainTarget);
    }

    /// <summary>
    /// Verifies a newer graph producing identical chain bytes preserves the output timestamp
    /// but advances processing completion, so the next build skips instead of regenerating forever.
    /// Also verifies graph edits and reverts update the chain when its contents actually change.
    /// </summary>
    [TestMethod]
    public void RuntimeIdentifierChainPreservesIdenticalResultsWithoutPerpetualRegeneration()
    {
        MetadataProject project = CreateProject();
        project.Build(ChainTarget);
        Dictionary<string, DateTime> unchanged = AgeAndSnapshot([project.ChainFile]);
        WriteNewer(project.GraphFile, Graph + Environment.NewLine, project.CompletionFile);

        project.Build(ChainTarget).Tasks.Should().Contain(ChainTarget);
        AssertTimestamps(unchanged);
        project.Build(ChainTarget).Tasks.Should().NotContain(ChainTarget);
        AssertTimestamps(unchanged);

        foreach (string graph in new[] { Graph.Replace("\"linux\"", "\"unix\""), Graph })
        {
            WriteNewer(project.GraphFile, graph, project.CompletionFile);
            project.Build(ChainTarget).Tasks.Should().Contain(ChainTarget);
            File.ReadAllText(project.ChainFile).Should().Be(graph == Graph ? "linux-x64\nlinux\nany" : "linux-x64\nunix\nany");
            project.Build(ChainTarget).Tasks.Should().NotContain(ChainTarget);
        }
    }

    private MetadataProject CreateProject() =>
        new(TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString("N")).Path);

    private static Dictionary<string, DateTime> Snapshot(IEnumerable<string> paths) =>
        paths.ToDictionary(path => path, File.GetLastWriteTimeUtc);

    private static Dictionary<string, DateTime> AgeAndSnapshot(IEnumerable<string> paths)
    {
        string[] files = paths.ToArray();
        foreach (string file in files)
        {
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(-5));
        }
        return Snapshot(files);
    }

    private static void AssertTimestamps(Dictionary<string, DateTime> timestamps)
    {
        foreach ((string path, DateTime timestamp) in timestamps)
        {
            File.GetLastWriteTimeUtc(path).Should().Be(timestamp, "{0} is unchanged", path);
        }
    }

    private static void AssertContents(Dictionary<string, byte[]> contents)
    {
        foreach ((string path, byte[] bytes) in contents)
        {
            File.ReadAllBytes(path).Should().Equal(bytes, "{0} has unchanged contents and encoding", path);
        }
    }

    private static string WriteFile(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    private static void WriteNewer(string path, string contents, string completion)
    {
        WriteFile(path, contents);
        MakeNewer(path, completion);
    }

    private static void MakeNewer(string path, string completion)
    {
        DateTime timestamp = File.GetLastWriteTimeUtc(completion);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        while (File.GetLastWriteTimeUtc(path) <= timestamp)
        {
            Thread.Sleep(20);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
    }

    private static string Escape(string value) => SecurityElement.Escape(value)!;

    private sealed record Observation(HashSet<string> Tasks);

    private sealed class MetadataProject
    {
        private readonly string _project;

        public MetadataProject(string root)
        {
            Root = root;
            PackageRoot = Path.Combine(root, "packages");
            GraphFile = WriteGraph(PackageRoot, "1.0.0", Graph);
            WriteFile(Path.Combine(PackageRoot, "microsoft.netcore.app.ref", "11.0.0", "data", "FrameworkList.xml"),
                """<FileList TargetFrameworkVersion="11.0" />""");
            WriteFile(Path.Combine(root, "minimumMSBuildVersion"), "17.14.0");
            WriteFile(Path.Combine(root, "eng", "Version.Details.xml"), """
                <Dependencies><ProductDependencies>
                  <Dependency Name="Microsoft.NETCore.App.Ref" Version="11.0.0"><Sha>runtime-commit</Sha></Dependency>
                  <Dependency Name="Microsoft.AspNetCore.App.Ref" Version="11.0.0"><Sha>aspnet-commit</Sha></Dependency>
                </ProductDependencies></Dependencies>
                """);
            foreach (string name in new[] { "GenerateLayout.targets", "GenerateBundledVersions.targets" })
            {
                string source = LayoutFiles.GetPath(name, Path.Combine("src", "Layout", "redist", "targets", name));
                File.Copy(source, Path.Combine(root, name));
            }
            string assembly = typeof(GenerateSdkRuntimeIdentifierChain).Assembly.Location;
            string builtIn = Path.Combine(Path.GetDirectoryName(typeof(Project).Assembly.Location)!, "Microsoft.Build.Tasks.Core.dll");
            string usingTasks = string.Join(Environment.NewLine,
                new[] { "GenerateSdkRuntimeIdentifierChain", "CompleteIncrementalLayout", "GetDependencyInfo", "GenerateDefaultRuntimeFrameworkVersion" }
                    .Select(name => $"<UsingTask TaskName=\"{name}\" AssemblyFile=\"{Escape(assembly)}\" />")
                    .Concat(new[] { "Copy", "Delete", "WriteLinesToFile", "ReadLinesFromFile", "XmlPeek", "Error" }
                        .Select(name => $"<UsingTask TaskName=\"Microsoft.Build.Tasks.{name}\" AssemblyFile=\"{Escape(builtIn)}\" />")));
            _project = WriteFile(Path.Combine(root, "metadata.proj"), $$"""
                <Project>
                  {{usingTasks}}
                  <PropertyGroup>
                    <OutputPath>{{Escape(Path.Combine(root, "out"))}}\</OutputPath>
                    <IntermediateOutputPath>{{Escape(Path.Combine(root, "obj"))}}\</IntermediateOutputPath>
                    <ArtifactsShippingPackagesDir>{{Escape(Path.Combine(root, "shipping"))}}\</ArtifactsShippingPackagesDir>
                    <RepositoryEngineeringDir>{{Escape(Path.Combine(root, "eng"))}}\</RepositoryEngineeringDir>
                    <NuGetPackageRoot>{{Escape(PackageRoot)}}\</NuGetPackageRoot>
                    <SdkTasksAssembly>{{Escape(assembly)}}</SdkTasksAssembly>
                    <MicrosoftNETCorePlatformsPackageVersion>1.0.0</MicrosoftNETCorePlatformsPackageVersion>
                    <MicrosoftNETCoreAppRefPackageVersion>11.0.0</MicrosoftNETCoreAppRefPackageVersion>
                    <MicrosoftNETCoreAppRuntimePackageVersion>11.0.0</MicrosoftNETCoreAppRuntimePackageVersion>
                    <MicrosoftAspNetCoreAppRefPackageVersion>11.0.0</MicrosoftAspNetCoreAppRefPackageVersion>
                    <MicrosoftAspNetCoreAppRuntimePackageVersion>11.0.0</MicrosoftAspNetCoreAppRuntimePackageVersion>
                    <MicrosoftWindowsDesktopAppRefPackageVersion>11.0.0</MicrosoftWindowsDesktopAppRefPackageVersion>
                    <MicrosoftWindowsDesktopAppRuntimePackageVersion>11.0.0</MicrosoftWindowsDesktopAppRuntimePackageVersion>
                    <NETStandardLibraryRefPackageVersion>2.1.0</NETStandardLibraryRefPackageVersion>
                    <MicrosoftBuildVersion>18.0.0-preview.1</MicrosoftBuildVersion>
                    <PortableProductMonikerRid>linux-x64</PortableProductMonikerRid>
                    <TargetRid>test</TargetRid>
                    <ProductMonikerRid>test</ProductMonikerRid>
                    <SourceRevisionId>source-revision</SourceRevisionId>
                    <BUILD_SOURCEVERSION>sdk-commit</BUILD_SOURCEVERSION>
                    <Version>11.0.100</Version>
                    <FullNugetVersion>11.0.100-test</FullNugetVersion>
                    <PackageVersion>11.0.100-test</PackageVersion>
                    <SdkFeatureBand>11.0.100</SdkFeatureBand>
                    <ObsoleteCliTool>Old.Test.Tool</ObsoleteCliTool>
                    <ManifestId>Test.Manifest</ManifestId>
                  </PropertyGroup>
                  <ItemGroup>
                    <BundledDotnetTool Include="test-tool" ObsoletesCliTool="$(ObsoleteCliTool)" />
                    <BundledManifests Include="$(ManifestId)" />
                    <BuiltinManifests Include="builtin.manifest" />
                  </ItemGroup>
                  <Import Project="GenerateLayout.targets" />
                  <Import Project="GenerateBundledVersions.targets" />
                </Project>
                """);
            Directory.CreateDirectory(Path.Combine(root, "out"));
            Directory.CreateDirectory(Path.Combine(root, "shipping"));
        }

        public string Root { get; }
        public string PackageRoot { get; }
        public string GraphFile { get; }
        public string ChainFile => Output("NETCoreSdkRuntimeIdentifierChain.txt");
        public string CompletionFile => Path.Combine(Root, "obj", "incremental-layout", "sdk-runtime-identifier-chain.complete");
        public string[] StateFiles => [Path.ChangeExtension(CompletionFile, ".inputs"), CompletionFile];
        public string[] MetadataFiles =>
        [
            ShippingFile("productVersion.txt"), ShippingFile("sdk-productVersion.txt"),
            ShippingFile("productCommit-test.txt"), ShippingFile("productCommit-test.json"),
            Output("Microsoft.NETCoreSdk.BundledVersions.props"), Output("Microsoft.NETCoreSdk.BundledCliTools.props"),
            Output("Microsoft.NETCoreSdk.BundledMSBuildInformation.props"),
            Output(".version"), Output(".toolsetversion"), Output("KnownWorkloadManifests.txt")
        ];
        public string Output(string name) => Path.Combine(Root, "out", name);
        public string ShippingFile(string name) => Path.Combine(Root, "shipping", name);

        public string WriteGraph(string root, string version, string contents)
        {
            string directory = Path.Combine(root, "microsoft.netcore.platforms", version);
            WriteFile(Path.Combine(directory, "runtime.json"), contents);
            return WriteFile(Path.Combine(directory, "PortableRuntimeIdentifierGraph.json"), contents);
        }

        public Observation Build(string targets, Dictionary<string, string>? properties = null, bool success = true)
        {
            using var collection = new ProjectCollection(properties ?? []);
            var logger = new MetadataLogger();
            SdkLayoutBuild.Run(collection, _project, Root, targets.Split(';'), logger, () => logger.Text, success);
            return new Observation(logger.Tasks);
        }
    }

    private sealed class MetadataLogger : Logger
    {
        private readonly List<string> _messages = [];
        public HashSet<string> Tasks { get; } = [];
        public string Text => string.Join(Environment.NewLine, _messages);

        public override void Initialize(IEventSource source)
        {
            source.TaskStarted += (_, args) => Tasks.Add(args.TaskName.Split('.').Last());
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
