// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
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
        DateTime originalTemplateTimestamp = File.GetLastWriteTimeUtc(project.Template);
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

        project.SwitchTemplateVersion("2.0.0");
        project.Build();
        Dictionary<string, string> updatedTemplate = project.ReadHosts();
        foreach (string host in Hosts)
        {
            updatedTemplate[host].Should().NotBe(repaired[host]);
        }

        project.SwitchTemplateVersion("1.0.0");
        File.GetLastWriteTimeUtc(project.Template).Should().Be(originalTemplateTimestamp);
        project.Build();
        Dictionary<string, string> revertedTemplate = project.ReadHosts();
        foreach (string host in Hosts)
        {
            revertedTemplate[host].Should().NotBe(updatedTemplate[host]);
        }

        project.Build();
        project.ReadHosts().Should().Equal(revertedTemplate);

        string[] inputFiles = Directory.GetFiles(Path.Combine(project.Root, "obj", "incremental-layout"), "*.inputs");
        Dictionary<string, DateTime> inputTimestamps = inputFiles.ToDictionary(path => path, File.GetLastWriteTimeUtc);
        File.WriteAllText(project.Template, "updated template at the same path");
        project.Build();
        Dictionary<string, string> updatedTemplateContents = project.ReadHosts();
        foreach (string host in Hosts)
        {
            updatedTemplateContents[host].Should().NotBe(revertedTemplate[host]);
        }
        foreach ((string path, DateTime timestamp) in inputTimestamps)
        {
            File.GetLastWriteTimeUtc(path).Should().Be(timestamp);
        }

        project.Build();
        project.ReadHosts().Should().Equal(updatedTemplateContents);

        File.WriteAllText(project.AssemblyPath("MSBuild"), "updated MSBuild.dll");
        project.Build();
        Dictionary<string, string> updatedAssembly = project.ReadHosts();
        updatedAssembly["MSBuild"].Should().NotBe(updatedTemplateContents["MSBuild"]);
        updatedAssembly["MSBuild"].Should().StartWith("MSBuild.dll|resources=updated MSBuild.dll|");
        foreach (string host in Hosts.Where(host => host != "MSBuild"))
        {
            updatedAssembly[host].Should().Be(updatedTemplateContents[host]);
        }

        File.WriteAllText(project.AssemblyPath("csc"), "updated csc.dll");
        project.Build();
        Dictionary<string, string> updatedCompiler = project.ReadHosts();
        updatedCompiler["csc"].Should().NotBe(updatedAssembly["csc"]);
        updatedCompiler["csc"].Should().StartWith("csc.dll|resources=updated csc.dll|");
        foreach (string host in Hosts.Where(host => host != "csc"))
        {
            updatedCompiler[host].Should().Be(updatedAssembly[host]);
        }

        File.WriteAllText(project.LayoutTaskAssembly, "updated sdk-tasks.dll");
        project.Build();
        project.ReadHosts().Should().Equal(updatedCompiler);

        File.WriteAllText(project.TaskAssembly, "updated Microsoft.NET.Build.Tasks.dll");
        project.Build();
        Dictionary<string, string> updatedTask = project.ReadHosts();
        foreach (string host in Hosts)
        {
            updatedTask[host].Should().NotBe(updatedCompiler[host]);
        }

        File.WriteAllText(project.HostModelAssembly, "updated Microsoft.NET.HostModel.dll");
        project.Build();
        Dictionary<string, string> updatedHostModel = project.ReadHosts();
        foreach (string host in Hosts)
        {
            updatedHostModel[host].Should().NotBe(updatedTask[host]);
        }

        project.Build();
        project.ReadHosts().Should().Equal(updatedHostModel);
    }

    [TestMethod]
    public void PackageChangesRegenerateHostsEvenWhenAssembliesAreOlder()
    {
        AppHostProject project = new(TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString("N")).Path);
        foreach (string host in Hosts)
        {
            File.SetLastWriteTimeUtc(project.AssemblyPath(host), DateTime.UtcNow.AddHours(-3));
        }
        project.Build();
        Dictionary<string, string> generated = project.ReadHosts();

        project.SwitchCompilerVersion("2.0.0");
        project.Build();
        Dictionary<string, string> updatedCompilers = project.ReadHosts();
        foreach (string host in Hosts.Where(host => host != "MSBuild"))
        {
            File.GetLastWriteTimeUtc(project.AssemblyPath(host)).Should().BeBefore(File.GetLastWriteTimeUtc(project.HostPath(host)));
            updatedCompilers[host].Should().NotBe(generated[host]);
            updatedCompilers[host].Should().StartWith($"{host}.dll|resources=replacement {host}.dll|");
        }
        updatedCompilers["MSBuild"].Should().Be(generated["MSBuild"]);
        project.Build();
        project.ReadHosts().Should().Equal(updatedCompilers);

        project.SwitchMSBuildVersion("2.0.0");
        project.Build();
        Dictionary<string, string> updatedMSBuild = project.ReadHosts();
        File.GetLastWriteTimeUtc(project.AssemblyPath("MSBuild")).Should().BeBefore(File.GetLastWriteTimeUtc(project.HostPath("MSBuild")));
        updatedMSBuild["MSBuild"].Should().NotBe(updatedCompilers["MSBuild"]);
        updatedMSBuild["MSBuild"].Should().StartWith("MSBuild.dll|resources=replacement MSBuild.dll|");
        foreach (string host in Hosts.Where(host => host != "MSBuild"))
        {
            updatedMSBuild[host].Should().Be(updatedCompilers[host]);
        }
        project.Build();
        project.ReadHosts().Should().Equal(updatedMSBuild);
    }

    /// <summary>
    /// Verifies Containers import files are copied without rewriting unchanged outputs
    /// while the child Publish still runs on every build.
    /// </summary>
    [TestMethod]
    public void ContainersImportsAreNotRewrittenWhenUnchanged()
    {
        AppHostProject project = new(TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString("N")).Path);
        project.Build("PublishContainersSdk").Should().Contain("Copying file from");
        string[] outputs = Directory.GetFiles(Path.Combine(project.OutputDirectory, "Containers", "build"));
        outputs.Should().HaveCount(2);
        Dictionary<string, DateTime> timestamps = outputs.ToDictionary(path => path, File.GetLastWriteTimeUtc);

        string warmBuild = project.Build("PublishContainersSdk");
        warmBuild.Should().NotContain("Copying file from");
        foreach ((string path, DateTime timestamp) in timestamps)
        {
            warmBuild.Should().Contain($"{Path.GetFileName(path)}\" because the \"SkipUnchangedFiles\" parameter was set to \"true\"");
            File.GetLastWriteTimeUtc(path).Should().Be(timestamp);
        }
        File.ReadAllLines(Path.Combine(project.Root, "container-publish.log")).Should().HaveCount(2);
    }

    [TestMethod]
    public void MissingAppHostTemplateFailsEvenWhenHostsExist()
    {
        AppHostProject project = new(TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString("N")).Path);
        project.Build();
        File.Delete(project.Template);

        project.Build(success: false).Should().Contain("Apphost template").And.Contain("was not found.");
    }

    [TestMethod]
    public void AppHostsUseLiteralSigningPathsAndRepairFailedSigning()
    {
        string root = Path.Combine(TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString("N")).Path,
            "signing paths $APPHOST_REVIEW_USER `printf expanded`");
        AppHostProject project = new(root, macOS: true, additionalCompilerHosts: ["extra"]);
        project.Build();
        Dictionary<string, string> generated = project.ReadHosts();
        generated.Values.Should().OnlyContain(text => text.EndsWith("|signed", StringComparison.Ordinal));
        generated.Values.Should().OnlyContain(text =>
            text.IndexOf("|signed", StringComparison.Ordinal) == text.LastIndexOf("|signed", StringComparison.Ordinal));
        generated.Values.Should().OnlyContain(text => !text.Contains("|ad-hoc-signed", StringComparison.Ordinal));
        foreach (string host in Hosts)
        {
            generated[host].Should().EndWith($"|identifier=com.microsoft.dotnet.{host.ToLowerInvariant()}|signed");
        }
        string extraHost = File.ReadAllText(project.HostPath("extra"));
        extraHost.Should().StartWith("extra.dll|").And.EndWith("|identifier=com.microsoft.dotnet.extra|signed");

        project.Build();
        project.ReadHosts().Should().Equal(generated);
        File.ReadAllText(project.HostPath("extra")).Should().Be(extraHost);

        File.Delete(project.HostPath("vbc"));
        string failureFile = Path.Combine(root, "fail-signing");
        File.WriteAllText(failureFile, "vbc");
        project.Build(success: false).Should().Contain("Simulated signing failure");
        File.Exists(project.HostPath("vbc")).Should().BeFalse();
        foreach (string host in Hosts.Where(host => host != "vbc"))
        {
            File.ReadAllText(project.HostPath(host)).Should().Be(generated[host]);
        }
        File.ReadAllText(project.HostPath("extra")).Should().Be(extraHost);

        File.Delete(failureFile);
        project.Build();
        Dictionary<string, string> repaired = project.ReadHosts();
        repaired["vbc"].Should().NotBe(generated["vbc"]).And.EndWith("|signed");
        foreach (string host in Hosts.Where(host => host != "vbc"))
        {
            repaired[host].Should().Be(generated[host]);
        }
        File.ReadAllText(project.HostPath("extra")).Should().Be(extraHost);

        File.Delete(project.HostPath("extra"));
        project.Build();
        File.ReadAllText(project.HostPath("extra")).Should().NotBe(extraHost).And.EndWith("|signed");
        project.ReadHosts().Should().Equal(repaired);

        File.Delete(project.HostPath("MSBuild"));
        File.WriteAllText(failureFile, "MSBuild");
        project.Build(success: false).Should().Contain("Simulated signing failure");
        File.Exists(project.HostPath("MSBuild")).Should().BeFalse();
        foreach (string host in Hosts.Where(host => host != "MSBuild"))
        {
            File.ReadAllText(project.HostPath(host)).Should().Be(repaired[host]);
        }

        File.Delete(failureFile);
        project.Build();
        Dictionary<string, string> repairedMSBuild = project.ReadHosts();
        repairedMSBuild["MSBuild"].Should().NotBe(repaired["MSBuild"]).And.EndWith("|signed");
        foreach (string host in Hosts.Where(host => host != "MSBuild"))
        {
            repairedMSBuild[host].Should().Be(repaired[host]);
        }

        project.Build();
        project.ReadHosts().Should().Equal(repairedMSBuild);

        project.SwitchSigningIdentifier("csc", "com.microsoft.dotnet.csc.updated");
        project.Build();
        Dictionary<string, string> updatedCompilerIdentifier = project.ReadHosts();
        updatedCompilerIdentifier["csc"].Should().NotBe(repairedMSBuild["csc"])
            .And.EndWith("|identifier=com.microsoft.dotnet.csc.updated|signed");
        foreach (string host in Hosts.Where(host => host != "csc"))
        {
            updatedCompilerIdentifier[host].Should().Be(repairedMSBuild[host]);
        }
        project.Build();
        project.ReadHosts().Should().Equal(updatedCompilerIdentifier);

        project.SwitchSigningIdentifier("MSBuild", "com.microsoft.dotnet.msbuild.updated");
        project.Build();
        Dictionary<string, string> updatedMSBuildIdentifier = project.ReadHosts();
        updatedMSBuildIdentifier["MSBuild"].Should().NotBe(updatedCompilerIdentifier["MSBuild"])
            .And.EndWith("|identifier=com.microsoft.dotnet.msbuild.updated|signed");
        foreach (string host in Hosts.Where(host => host != "MSBuild"))
        {
            updatedMSBuildIdentifier[host].Should().Be(updatedCompilerIdentifier[host]);
        }
        project.Build();
        project.ReadHosts().Should().Equal(updatedMSBuildIdentifier);
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
        private string _compilerVersion = "1.0.0";
        private string _templateVersion = "1.0.0";

        public AppHostProject(string root, bool macOS = false, params string[] additionalCompilerHosts)
        {
            Root = root;
            ExeExtension = macOS ? string.Empty : ".exe";
            string runtimeIdentifier = macOS ? "osx-x64" : "win-x64";
            string packages = Path.Combine(root, "packages") + Path.DirectorySeparatorChar;
            Template = Path.Combine(packages, $"microsoft.netcore.app.host.{runtimeIdentifier}", "1.0.0", "runtimes", runtimeIdentifier, "native", "apphost" + ExeExtension);
            Write(Template, "template");
            File.SetLastWriteTimeUtc(Template, DateTime.UtcNow.AddHours(-1));
            TypeLibrary = Path.Combine(OutputDirectory, "Microsoft.Build.Framework.tlb");
            LayoutTaskAssembly = Path.Combine(root, "sdk-tasks.dll");
            TaskAssembly = Path.Combine(root, "sdk-tasks", "Microsoft.NET.Build.Tasks.dll");
            HostModelAssembly = Path.Combine(root, "sdk-tasks", "Microsoft.NET.HostModel.dll");
            Write(Path.Combine(packages, "microsoft.build.framework", "1.0.0", "tools", "net472", "Microsoft.Build.Framework.tlb"), "tlb");
            Write(Path.Combine(OutputDirectory, "MSBuild.dll"), "MSBuild.dll");
            Write(LayoutTaskAssembly, "sdk-tasks.dll");
            Write(TaskAssembly, "Microsoft.NET.Build.Tasks.dll");
            Write(HostModelAssembly, "Microsoft.NET.HostModel.dll");
            if (macOS)
            {
                foreach (string name in new[] { "roslyn-entitlements.plist", "msbuild-entitlements.plist" })
                {
                    File.Copy(LayoutFiles.GetPath(name, Path.Combine("src", "Layout", "redist", name)), Path.Combine(root, name));
                }
            }
            string[] compilerHosts = [.. Hosts.Where(host => host != "MSBuild"), .. additionalCompilerHosts];
            foreach (string name in compilerHosts)
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
                new[] { "Copy", "Delete", "MSBuild" }
                    .Select(name => $"<UsingTask TaskName=\"Microsoft.Build.Tasks.{name}\" AssemblyFile=\"{Escape(BuiltInTasks)}\" />")
                    .Append($"<UsingTask TaskName=\"{typeof(Exec).FullName}\" AssemblyFile=\"{Escape(typeof(Exec).Assembly.Location)}\" />")
                    .Append($"<UsingTask TaskName=\"{typeof(CreateAppHost).FullName}\" AssemblyFile=\"{Escape(typeof(CreateAppHost).Assembly.Location)}\" />")
                    .Append($"<UsingTask TaskName=\"UpdateRuntimeConfig\" AssemblyFile=\"{Escape(typeof(Microsoft.DotNet.Build.Tasks.UpdateRuntimeConfig).Assembly.Location)}\" />"));
            _project = Path.Combine(root, "apphost-layout.proj");
            Write(_project, $$"""
                <Project>
                  {{usingTasks}}
                  <PropertyGroup>
                    <RepoRoot>{{Escape(root + Path.DirectorySeparatorChar)}}</RepoRoot>
                    <OutputPath>{{Escape(OutputDirectory + Path.DirectorySeparatorChar)}}</OutputPath>
                    <IntermediateOutputPath>{{Escape(Path.Combine(root, "obj") + Path.DirectorySeparatorChar)}}</IntermediateOutputPath>
                    <SdkTasksAssembly>{{Escape(LayoutTaskAssembly)}}</SdkTasksAssembly>
                    <MicrosoftNETBuildTasksDirectory>{{Escape(Path.GetDirectoryName(TaskAssembly) + Path.DirectorySeparatorChar)}}</MicrosoftNETBuildTasksDirectory>
                    <MicrosoftNETBuildTasksAssembly>{{Escape(TaskAssembly)}}</MicrosoftNETBuildTasksAssembly>
                    <NuGetPackageRoot>{{Escape(packages)}}</NuGetPackageRoot>
                    <SharedFrameworkRid>{{runtimeIdentifier}}</SharedFrameworkRid>
                    <ExeExtension>{{ExeExtension}}</ExeExtension>
                    <MicrosoftNETCoreAppHostPackageVersion>1.0.0</MicrosoftNETCoreAppHostPackageVersion>
                    <MicrosoftNetCompilersToolsetVersion>1.0.0</MicrosoftNetCompilersToolsetVersion>
                    <MicrosoftBuildVersion>1.0.0</MicrosoftBuildVersion>
                    <Configuration>Debug</Configuration>
                    <SdkTargetFramework>test-framework</SdkTargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    {{string.Join(Environment.NewLine, compilerHosts.Select(name => $"<_RoslynAppHost Include=\"$(OutputPath)Roslyn\\bincore\\{Escape(name)}.dll\" SigningIdentifier=\"com.microsoft.dotnet.{Escape(name.ToLowerInvariant())}\" />"))}}
                    <_MSBuildAppHost Include="$(OutputPath)MSBuild.dll" SigningIdentifier="com.microsoft.dotnet.msbuild" />
                  </ItemGroup>
                  <Import Project="GenerateLayout.targets" />
                </Project>
                """);
        }

        public string Root { get; }
        public string Template { get; private set; }
        public string TypeLibrary { get; }
        public string LayoutTaskAssembly { get; }
        public string TaskAssembly { get; }
        public string HostModelAssembly { get; }
        private string ExeExtension { get; }
        public string OutputDirectory => Path.Combine(Root, "out");

        public string AssemblyPath(string name) =>
            name == "MSBuild" ? Path.Combine(OutputDirectory, "MSBuild.dll") : Path.Combine(Root, "packages", "microsoft.net.compilers.toolset", _compilerVersion, "tasks", "netcore", "bincore", name + ".dll");

        public void SwitchSigningIdentifier(string host, string identifier)
        {
            File.WriteAllText(_project, File.ReadAllText(_project).Replace(
                $"SigningIdentifier=\"com.microsoft.dotnet.{host.ToLowerInvariant()}\"",
                $"SigningIdentifier=\"{Escape(identifier)}\"", StringComparison.Ordinal));
        }

        public void SwitchCompilerVersion(string version)
        {
            File.WriteAllText(_project, File.ReadAllText(_project).Replace(
                $"<MicrosoftNetCompilersToolsetVersion>{_compilerVersion}</MicrosoftNetCompilersToolsetVersion>",
                $"<MicrosoftNetCompilersToolsetVersion>{version}</MicrosoftNetCompilersToolsetVersion>", StringComparison.Ordinal));
            _compilerVersion = version;
            foreach (string name in Hosts.Where(host => host != "MSBuild"))
            {
                Write(AssemblyPath(name), $"replacement {name}.dll");
                File.SetLastWriteTimeUtc(AssemblyPath(name), DateTime.UtcNow.AddHours(-2));
            }
        }

        public void SwitchMSBuildVersion(string version)
        {
            File.WriteAllText(_project, File.ReadAllText(_project).Replace(
                "<MicrosoftBuildVersion>1.0.0</MicrosoftBuildVersion>",
                $"<MicrosoftBuildVersion>{version}</MicrosoftBuildVersion>", StringComparison.Ordinal));
            // The redist build stages the package assembly before calling the layout targets.
            File.WriteAllText(AssemblyPath("MSBuild"), "replacement MSBuild.dll");
            File.SetLastWriteTimeUtc(AssemblyPath("MSBuild"), DateTime.UtcNow.AddHours(-2));
        }

        public void SwitchTemplateVersion(string version)
        {
            string oldVersion = $"<MicrosoftNETCoreAppHostPackageVersion>{_templateVersion}</MicrosoftNETCoreAppHostPackageVersion>";
            string newVersion = $"<MicrosoftNETCoreAppHostPackageVersion>{version}</MicrosoftNETCoreAppHostPackageVersion>";
            File.WriteAllText(_project, File.ReadAllText(_project).Replace(oldVersion, newVersion, StringComparison.Ordinal));
            _templateVersion = version;
            Template = Path.Combine(Root, "packages", $"microsoft.netcore.app.host.win-x64", version, "runtimes", "win-x64", "native", "apphost.exe");
            if (!File.Exists(Template))
            {
                Write(Template, "replacement template");
                File.SetLastWriteTimeUtc(Template, DateTime.UtcNow.AddHours(-2));
            }
        }

        public Dictionary<string, string> ReadHosts() => Hosts.ToDictionary(host => host, host => File.ReadAllText(HostPath(host)));

        public string HostPath(string name) =>
            name == "MSBuild" ? Path.Combine(OutputDirectory, name + ExeExtension) : Path.Combine(OutputDirectory, "Roslyn", "bincore", name + ExeExtension);

        public string Build(string targets = "PublishRoslyn;CreateMSBuildAppHost", bool success = true)
        {
            using var collection = new ProjectCollection();
            var logger = new RecordingLogger();
            SdkLayoutBuild.Run(collection, _project, Root, targets.Split(';'), logger, () => logger.Text, success);
            return logger.Text;
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
/// Stands in for the SDK apphost writer: each invocation writes the embedded binary name,
/// managed resource input, and a unique token, so tests can tell which hosts were regenerated.
/// </summary>
public sealed class CreateAppHost : Microsoft.Build.Utilities.Task
{
    [Required]
    public string AppHostSourcePath { get; set; } = string.Empty;

    [Required]
    public string AppHostDestinationPath { get; set; } = string.Empty;

    [Required]
    public string AppBinaryName { get; set; } = string.Empty;

    [Required]
    public string IntermediateAssembly { get; set; } = string.Empty;

    public bool EnableMacOSCodeSign { get; set; }

    public override bool Execute()
    {
        if (!File.Exists(AppHostSourcePath))
        {
            Log.LogError($"Apphost source '{AppHostSourcePath}' does not exist.");
            return false;
        }

        if (!File.Exists(IntermediateAssembly))
        {
            Log.LogError($"Intermediate assembly '{IntermediateAssembly}' does not exist.");
            return false;
        }

        string signature = EnableMacOSCodeSign ? "|ad-hoc-signed" : string.Empty;
        File.WriteAllText(AppHostDestinationPath, $"{AppBinaryName}|resources={File.ReadAllText(IntermediateAssembly)}|{Guid.NewGuid():N}{signature}");
        return true;
    }
}

public sealed class Exec : Microsoft.Build.Utilities.Task
{
    [Required]
    public string Command { get; set; } = string.Empty;

    [Required]
    public string[] EnvironmentVariables { get; set; } = [];

    public override bool Execute()
    {
        const string entitlementsPrefix = "_SDK_APPHOST_ENTITLEMENTS=";
        const string hostPrefix = "_SDK_APPHOST_PATH=";
        const string identifierPrefix = "_SDK_APPHOST_IDENTIFIER=";
        if (Command != "codesign --sign - --force --preserve-metadata=entitlements,requirements,flags,runtime,launch-constraints,library-constraints --identifier \"$_SDK_APPHOST_IDENTIFIER\" --entitlements \"$_SDK_APPHOST_ENTITLEMENTS\" \"$_SDK_APPHOST_PATH\"" ||
            EnvironmentVariables.Length != 3 ||
            !EnvironmentVariables[0].StartsWith(entitlementsPrefix, StringComparison.Ordinal) ||
            !EnvironmentVariables[1].StartsWith(hostPrefix, StringComparison.Ordinal) ||
            !EnvironmentVariables[2].StartsWith(identifierPrefix, StringComparison.Ordinal))
        {
            Log.LogError($"Invalid signing command or environment: {Command}");
            return false;
        }

        string entitlements = EnvironmentVariables[0][entitlementsPrefix.Length..];
        string host = EnvironmentVariables[1][hostPrefix.Length..];
        string identifier = EnvironmentVariables[2][identifierPrefix.Length..];
        if (!File.Exists(entitlements) || !File.Exists(host) || string.IsNullOrWhiteSpace(identifier))
        {
            Log.LogError($"Invalid signing input: '{entitlements}', '{host}', or identifier '{identifier}'.");
            return false;
        }

        if (!OperatingSystem.IsWindows())
        {
            ProcessStartInfo startInfo = new("/bin/sh") { UseShellExecute = false };
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("""
                codesign() {
                    test "$#" -eq 9 &&
                    test "$4" = "--preserve-metadata=entitlements,requirements,flags,runtime,launch-constraints,library-constraints" &&
                    test "$5" = "--identifier" &&
                    test "$6" = "$_SDK_APPHOST_IDENTIFIER" &&
                    test "$7" = "--entitlements" &&
                    test "$8" = "$_SDK_APPHOST_ENTITLEMENTS" &&
                    test "$9" = "$_SDK_APPHOST_PATH"
                }
                """ + "\n" + Command);
            startInfo.Environment["_SDK_APPHOST_ENTITLEMENTS"] = entitlements;
            startInfo.Environment["_SDK_APPHOST_PATH"] = host;
            startInfo.Environment["_SDK_APPHOST_IDENTIFIER"] = identifier;
            using Process process = new() { StartInfo = startInfo };
            process.Start();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                Log.LogError("Signing command did not preserve literal argument values.");
                return false;
            }
        }

        string failureFile = Path.Combine(Path.GetDirectoryName(entitlements)!, "fail-signing");
        if (File.Exists(failureFile) && File.ReadAllText(failureFile) == Path.GetFileName(host))
        {
            Log.LogError("Simulated signing failure");
            return false;
        }

        File.AppendAllText(host, $"|identifier={identifier}|signed");
        return true;
    }
}
