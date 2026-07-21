// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Runtime.CompilerServices;
using System.Xml;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Utils;
using PackCommand = Microsoft.DotNet.Cli.Commands.Pack.PackCommand;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
public class MSBuildEvaluationTests
{
    private readonly struct SdkDirectoryScope : IDisposable
    {
        private readonly object? _previousSdkRoot = AppContext.GetData(SdkPaths.DataName);

        public SdkDirectoryScope(string sdkDirectory)
        {
            AppContext.SetData(SdkPaths.DataName, sdkDirectory);
            SdkPaths.ClearSdkDirectoryCacheForTests();
        }

        public void Dispose()
        {
            AppContext.SetData(SdkPaths.DataName, _previousSdkRoot);
            SdkPaths.ClearSdkDirectoryCacheForTests();
        }
    }

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void UsesNuGetAotFeatureSwitch()
    {
        Assert.IsTrue(AppContext.TryGetSwitch("NuGet.UseSystemTextJsonDeserialization", out bool useSystemTextJson));
        Assert.IsTrue(useSystemTextJson);
    }

    [TestMethod]
    public void NativeAotUsesProductionMSBuildFeatureSwitches()
    {
        if (RuntimeFeature.IsDynamicCodeSupported)
        {
            return;
        }

        Assert.IsTrue(AppContext.TryGetSwitch("Microsoft.Build.EnableSdkResolverDynamicLoading", out bool dynamicResolverLoading));
        Assert.IsFalse(dynamicResolverLoading);
        Assert.IsTrue(AppContext.TryGetSwitch("Microsoft.Build.EnableAllPropertyFunctions", out bool allPropertyFunctions));
        Assert.IsFalse(allPropertyFunctions);
        Assert.IsTrue(AppContext.TryGetSwitch("Microsoft.Build.RestrictPropertyFunctionReceivers", out bool restrictedPropertyFunctions));
        Assert.IsTrue(restrictedPropertyFunctions);
    }

    [TestMethod]
    public void MSBuildForwardingUsesVersionedSdkDirectory()
    {
        string sdkDirectory = Path.Combine(Path.GetTempPath(), "dotnet", "sdk", "test-version");
        using var _ = new SdkDirectoryScope(sdkDirectory + Path.DirectorySeparatorChar);

        var forwardingApp = new MSBuildForwardingAppWithoutLogging(
            MSBuildArgs.FromOtherArgs(),
            forceOutOfProc: true);

        Assert.AreEqual(Path.Combine(sdkDirectory, "MSBuild.dll"), forwardingApp.MSBuildPath);
        Assert.AreEqual(
            Path.Combine(sdkDirectory, "Sdks"),
            forwardingApp.GetProcessStartInfo().Environment["MSBuildSDKsPath"]);
        Assert.AreEqual(
            sdkDirectory,
            forwardingApp.GetProcessStartInfo().Environment["MSBuildExtensionsPath"]);
    }

    [TestMethod]
    public void PackCommandEvaluatesPackReleaseAndForwardsToVersionedSdk()
    {
        string sdkDirectory = GetRequiredSdkDirectory();

        string testDirectory = Path.Combine(Path.GetTempPath(), $"aot-pack-{Guid.NewGuid():N}");
        string projectPath = Path.Combine(testDirectory, "TestProject.csproj");
        string binlogPath = Path.Combine(testDirectory, "pack.binlog");
        string previousCurrentDirectory = Directory.GetCurrentDirectory();
        using var _ = new SdkDirectoryScope(sdkDirectory);

        Directory.CreateDirectory(testDirectory);
        File.WriteAllText(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net11.0</TargetFramework>
                <PackRelease>true</PackRelease>
              </PropertyGroup>
            </Project>
            """);

        try
        {
            Directory.SetCurrentDirectory(testDirectory);
            MSBuildSdkResolverRegistration.Register();

            var command = (PackCommand)PackCommand.FromArgs(
                [projectPath, "--no-restore", $"-bl:{binlogPath}"]);
            string[] arguments = command.GetArgumentTokensToMSBuild();

            Assert.Contains("--target:Pack", arguments);
            Assert.Contains("--property:_IsPacking=true", arguments);
            Assert.Contains("--property:Configuration=Release", arguments);
            Assert.Contains($"-bl:{binlogPath}", arguments);
            Assert.DoesNotContain("-restore", arguments);
            Assert.Contains(
                Path.Combine(sdkDirectory, "MSBuild.dll"),
                command.GetProcessStartInfo().Arguments);

            var restoreCommand = (PackCommand)PackCommand.FromArgs([projectPath]);
            Assert.Contains("-restore", restoreCommand.GetArgumentTokensToMSBuild());

            var explicitConfigurationCommand = (PackCommand)PackCommand.FromArgs(
                [projectPath, "--no-restore", "--configuration", "Debug"]);
            string[] explicitConfigurationArguments = explicitConfigurationCommand.GetArgumentTokensToMSBuild();
            Assert.Contains("--property:Configuration=Debug", explicitConfigurationArguments);
            Assert.Contains(
                "--property:DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE=true",
                explicitConfigurationArguments);
            Assert.DoesNotContain("--property:Configuration=Release", explicitConfigurationArguments);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCurrentDirectory);
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void ProjectCompletionsEvaluateCurrentProject()
    {
        string sdkDirectory = GetRequiredSdkDirectory();

        string testDirectory = Path.Combine(Path.GetTempPath(), $"aot-pack-completion-{Guid.NewGuid():N}");
        string projectPath = Path.Combine(testDirectory, "TestProject.csproj");
        string previousCurrentDirectory = Directory.GetCurrentDirectory();
        using var _ = new SdkDirectoryScope(sdkDirectory);

        Directory.CreateDirectory(testDirectory);
        File.WriteAllText(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net11.0</TargetFramework>
                <Configurations>CustomDebug;CustomRelease</Configurations>
              </PropertyGroup>
            </Project>
            """);

        try
        {
            Directory.SetCurrentDirectory(testDirectory);
            MSBuildSdkResolverRegistration.Register();

            string[] completions = [.. Parser.Parse("pack --configuration ").GetCompletions().Select(item => item.Label)];
            string[] targetFrameworks = [.. CliCompletion.TargetFrameworksFromProjectFile(null!).Select(item => item.Label)];

            completions.Should().Equal("CustomDebug", "CustomRelease");
            targetFrameworks.Should().Equal("net11.0");
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCurrentDirectory);
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void PackCommandContinuesWhenPackReleaseEvaluationFails()
    {
        string testDirectory = Path.Combine(Path.GetTempPath(), $"aot-pack-evaluation-error-{Guid.NewGuid():N}");
        string projectPath = Path.Combine(testDirectory, "Invalid.csproj");

        Directory.CreateDirectory(testDirectory);
        File.WriteAllText(projectPath, "<Project");

        try
        {
            var command = Assert.IsInstanceOfType<PackCommand>(
                PackCommand.FromArgs([projectPath, "--no-restore"]));
            Assert.DoesNotContain("--property:Configuration=Release", command.GetArgumentTokensToMSBuild());
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void PackCommandContinuesWhenSolutionProjectEvaluationFails()
    {
        string testDirectory = Path.Combine(Path.GetTempPath(), $"aot-pack-solution-evaluation-error-{Guid.NewGuid():N}");
        string projectPath = Path.Combine(testDirectory, "Invalid.csproj");
        string solutionPath = Path.Combine(testDirectory, "Invalid.slnx");

        Directory.CreateDirectory(testDirectory);
        File.WriteAllText(projectPath, "<Project");
        File.WriteAllText(
            solutionPath,
            """
            <Solution>
              <Project Path="Invalid.csproj" />
            </Solution>
            """);

        try
        {
            var command = Assert.IsInstanceOfType<PackCommand>(
                PackCommand.FromArgs([solutionPath, "--no-restore"]));
            Assert.DoesNotContain("--property:Configuration=Release", command.GetArgumentTokensToMSBuild());
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void StockSdkProjectCanBeEvaluated()
    {
        string sdkDirectory = GetRequiredSdkDirectory();

        string binlogPath = Path.Combine(
            Path.GetTempPath(),
            $"aot-eval-{TestContext.TestName}-{Guid.NewGuid():N}.binlog");
        TestContext.WriteLine($"MSBuild evaluation binlog: {binlogPath}");

        using var _ = new SdkDirectoryScope(sdkDirectory);
        try
        {
            MSBuildSdkResolverRegistration.Register();

            var projectContent = new StringReader(
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net11.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            var binlog = new BinaryLogger
            {
                Parameters = binlogPath
            };
            using var projectCollection = new ProjectCollection(
                globalProperties: new Dictionary<string, string>
                {
                    // since this test is under the repo root, it wants to glob for repo-level Directory.Build.props/targets, which we don't want to do. Disable those imports.
                    ["ImportDirectoryBuildProps"] = "false",
                    ["ImportDirectoryBuildTargets"] = "false",
                },
                loggers: [binlog],
                toolsetDefinitionLocations: ToolsetDefinitionLocations.Default
            );
            var project = Project.FromXmlReader(
                XmlReader.Create(projectContent),
                new()
                {
                    ProjectCollection = projectCollection,
                    EvaluationStage = ProjectEvaluationStage.Properties,
                });
            Assert.AreEqual("net11.0", project.GetPropertyValue("TargetFramework"));
            Assert.Contains(import =>
                import.ImportedProject.FullPath.EndsWith(
                    Path.Combine("Microsoft.NET.Sdk", "Sdk", "Sdk.targets"),
                    StringComparison.OrdinalIgnoreCase),
                project.Imports);
        }
        finally
        {
            if (File.Exists(binlogPath))
            {
                TestContext.AddResultFile(binlogPath);
            }
        }
    }

    private static string GetRequiredSdkDirectory()
    {
        string? sdkDirectory = Environment.GetEnvironmentVariable("DOTNET_AOT_TEST_SDK_DIRECTORY");
        if (string.IsNullOrEmpty(sdkDirectory))
        {
            Assert.Inconclusive("DOTNET_AOT_TEST_SDK_DIRECTORY must identify the SDK used for Native AOT validation.");
        }

        return sdkDirectory;
    }
}
