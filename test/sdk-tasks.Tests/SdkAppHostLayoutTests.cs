// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.CoreSdkTasks.Tests;

/// <summary>
/// Exercises the production apphost and copy producers with an in-process stand-in for
/// the apphost writer, so only target orchestration is under test.
/// </summary>
[TestClass]
// In-process MSBuild saves/restores process-wide state used by other layout tests, and
// the layout tests share process-wide environment.
[DoNotParallelize]
public class SdkAppHostLayoutTests : SdkTest
{
    private static readonly string[] Hosts = ["csc", "vbc", "VBCSCompiler", "MSBuild"];

    /// <summary>
    /// Verifies a settled apphost is never rewritten, a missing or outdated one is repaired
    /// individually, and the optional type library is repaired independent of generation.
    /// </summary>
    [TestMethod]
    public void AppHostsAreGeneratedPerHostAndTypeLibraryIsIndependent()
    {
        AppHostProject project = new(TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString("N")).Path);
        project.Build();
        Dictionary<string, string> generated = project.ReadHosts();
        generated.Values.Should().OnlyHaveUniqueItems().And.OnlyContain(text => text.Length > 0);
        generated["csc"].Should().StartWith("csc.dll|");

        project.Build();
        project.ReadHosts().Should().Equal(generated);

        File.Delete(project.HostPath("vbc"));
        File.Delete(project.TypeLibrary);
        project.Build();
        Dictionary<string, string> repaired = project.ReadHosts();
        repaired["vbc"].Should().NotBe(generated["vbc"]);
        foreach (string host in Hosts.Where(host => host != "vbc"))
        {
            repaired[host].Should().Be(generated[host]);
        }
        File.Exists(project.TypeLibrary).Should().BeTrue();

        File.SetLastWriteTimeUtc(project.Template, DateTime.UtcNow.AddHours(1));
        project.Build();
        Dictionary<string, string> regenerated = project.ReadHosts();
        foreach (string host in Hosts)
        {
            regenerated[host].Should().NotBe(repaired[host]);
        }
    }
    /// <summary>
    /// Verifies Containers import files are copied without rewriting unchanged outputs
    /// while the child Publish still runs on every build.
    /// </summary>
    [TestMethod]
    public void ContainersImportsAreNotRewrittenWhenUnchanged()
    {
        AppHostProject project = new(TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString("N")).Path);
        project.Build("PublishContainersSdk");
        string[] outputs = Directory.GetFiles(Path.Combine(project.OutputDirectory, "Containers", "build"));
        outputs.Should().HaveCount(2);
        Dictionary<string, DateTime> timestamps = outputs.ToDictionary(path => path, File.GetLastWriteTimeUtc);

        project.Build("PublishContainersSdk");
        foreach ((string path, DateTime timestamp) in timestamps)
        {
            File.GetLastWriteTimeUtc(path).Should().Be(timestamp);
        }
        File.ReadAllLines(Path.Combine(project.Root, "container-publish.log")).Should().HaveCount(2);
    }

    private static string Escape(string value) => SecurityElement.Escape(value)!;

    private static void Write(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private sealed class AppHostProject
    {
        private readonly string _project;

        public AppHostProject(string root)
        {
            Root = root;
            string packages = Path.Combine(root, "packages") + Path.DirectorySeparatorChar;
            Template = Path.Combine(packages, "microsoft.netcore.app.host.win-x64", "1.0.0", "runtimes", "win-x64", "native", "apphost.exe");
            Write(Template, "template");
            File.SetLastWriteTimeUtc(Template, DateTime.UtcNow.AddHours(-1));
            TypeLibrary = Path.Combine(OutputDirectory, "Microsoft.Build.Framework.tlb");
            Write(Path.Combine(packages, "microsoft.build.framework", "1.0.0", "tools", "net472", "Microsoft.Build.Framework.tlb"), "tlb");
            Write(Path.Combine(OutputDirectory, "MSBuild.dll"), "MSBuild.dll");
            foreach (string name in new[] { "csc", "vbc", "VBCSCompiler" })
            {
                Write(Path.Combine(packages, "microsoft.net.compilers.toolset", "1.0.0", "tasks", "netcore", "bincore", name + ".dll"), name + ".dll");
            }

            Write(Path.Combine(root, "src", "Containers", "packaging", "build", "Microsoft.NET.Build.Containers.props"), "props");
            Write(Path.Combine(root, "src", "Containers", "packaging", "build", "Microsoft.NET.Build.Containers.targets"), "targets");
            Write(Path.Combine(root, "src", "Containers", "Microsoft.NET.Build.Containers", "Microsoft.NET.Build.Containers.csproj"), $$"""
                <Project>
                  <UsingTask TaskName="Microsoft.Build.Tasks.WriteLinesToFile" AssemblyFile="{{Escape(BuiltInTasks)}}" />
                  <Target Name="Publish">
                    <WriteLinesToFile File="{{Escape(Path.Combine(root, "container-publish.log"))}}" Lines="publish" />
                  </Target>
                </Project>
                """);

            string source = LayoutFiles.GetPath("GenerateLayout.targets",
                Path.Combine("src", "Layout", "redist", "targets", "GenerateLayout.targets"));
            File.Copy(source, Path.Combine(root, "GenerateLayout.targets"));
            File.SetLastWriteTimeUtc(Path.Combine(root, "GenerateLayout.targets"), DateTime.UtcNow.AddHours(-1));
            string usingTasks = string.Join(Environment.NewLine,
                new[] { "Copy", "Delete", "Exec", "MSBuild" }
                    .Select(name => $"<UsingTask TaskName=\"Microsoft.Build.Tasks.{name}\" AssemblyFile=\"{Escape(BuiltInTasks)}\" />")
                    .Append($"<UsingTask TaskName=\"{typeof(CreateAppHost).FullName}\" AssemblyFile=\"{Escape(typeof(CreateAppHost).Assembly.Location)}\" />")
                    .Append($"<UsingTask TaskName=\"UpdateRuntimeConfig\" AssemblyFile=\"{Escape(typeof(Microsoft.DotNet.Build.Tasks.UpdateRuntimeConfig).Assembly.Location)}\" />"));
            _project = Path.Combine(root, "apphost-layout.proj");
            Write(_project, $$"""
                <Project>
                  {{usingTasks}}
                  <PropertyGroup>
                    <RepoRoot>{{Escape(root + Path.DirectorySeparatorChar)}}</RepoRoot>
                    <OutputPath>{{Escape(OutputDirectory + Path.DirectorySeparatorChar)}}</OutputPath>
                    <NuGetPackageRoot>{{Escape(packages)}}</NuGetPackageRoot>
                    <SharedFrameworkRid>win-x64</SharedFrameworkRid>
                    <ExeExtension>.exe</ExeExtension>
                    <MicrosoftNETCoreAppHostPackageVersion>1.0.0</MicrosoftNETCoreAppHostPackageVersion>
                    <MicrosoftNetCompilersToolsetVersion>1.0.0</MicrosoftNetCompilersToolsetVersion>
                    <MicrosoftBuildVersion>1.0.0</MicrosoftBuildVersion>
                    <Configuration>Debug</Configuration>
                    <SdkTargetFramework>test-framework</SdkTargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <_RoslynAppHost Include="$(OutputPath)Roslyn\bincore\csc.dll" />
                    <_RoslynAppHost Include="$(OutputPath)Roslyn\bincore\vbc.dll" />
                    <_RoslynAppHost Include="$(OutputPath)Roslyn\bincore\VBCSCompiler.dll" />
                    <_MSBuildAppHost Include="$(OutputPath)MSBuild.dll" />
                  </ItemGroup>
                  <Import Project="GenerateLayout.targets" />
                </Project>
                """);
        }

        public string Root { get; }
        public string Template { get; }
        public string TypeLibrary { get; }
        public string OutputDirectory => Path.Combine(Root, "out");

        public Dictionary<string, string> ReadHosts() => Hosts.ToDictionary(host => host, host => File.ReadAllText(HostPath(host)));

        public string HostPath(string name) =>
            name == "MSBuild" ? Path.Combine(OutputDirectory, "MSBuild.exe") : Path.Combine(OutputDirectory, "Roslyn", "bincore", name + ".exe");

        public void Build(string targets = "PublishRoslyn;CreateMSBuildAppHost")
        {
            using var collection = new ProjectCollection();
            var logger = new RecordingLogger();
            SdkLayoutBuild.Run(collection, _project, Root, targets.Split(';'), logger, () => logger.Text);
        }

        private static string BuiltInTasks =>
            Path.Combine(Path.GetDirectoryName(typeof(Project).Assembly.Location)!, "Microsoft.Build.Tasks.Core.dll");
    }

    private sealed class RecordingLogger : Logger
    {
        private readonly List<string> _messages = [];
        public string Text => string.Join(Environment.NewLine, _messages);

        public override void Initialize(IEventSource source) =>
            source.AnyEventRaised += (_, args) =>
            {
                if (args.Message is not null)
                {
                    _messages.Add(args.Message);
                }
            };
    }
}

/// <summary>
/// Stands in for the SDK apphost writer: each invocation writes the embedded binary name\r\n/// and a unique token, so tests can tell which hosts were regenerated.
/// </summary>
public sealed class CreateAppHost : Microsoft.Build.Utilities.Task
{
    [Required]
    public string AppHostSourcePath { get; set; } = string.Empty;

    [Required]
    public string AppHostDestinationPath { get; set; } = string.Empty;

    [Required]
    public string AppBinaryName { get; set; } = string.Empty;

    public string IntermediateAssembly { get; set; } = string.Empty;

    public bool EnableMacOSCodeSign { get; set; }

    public override bool Execute()
    {
        File.WriteAllText(AppHostDestinationPath, $"{AppBinaryName}|{Guid.NewGuid():N}");
        return true;
    }
}
