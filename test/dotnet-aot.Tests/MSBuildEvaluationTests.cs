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

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
[ResourceLock(nameof(SdkDirectoryScope))]
public class MSBuildEvaluationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void UsesAotFeatureSwitches()
    {
        Assert.IsTrue(AppContext.TryGetSwitch("NuGet.UseSystemTextJsonDeserialization", out bool useSystemTextJson));
        Assert.IsTrue(useSystemTextJson);
        Assert.IsTrue(AppContext.TryGetSwitch("Newtonsoft.Json.Linq.JToken.SerializationIsSupported", out bool jsonSerialization));
        Assert.IsFalse(jsonSerialization);
        Assert.IsTrue(AppContext.TryGetSwitch("Newtonsoft.Json.Linq.JToken.ComponentModelIsSupported", out bool jsonComponentModel));
        Assert.IsFalse(jsonComponentModel);
        Assert.IsTrue(AppContext.TryGetSwitch("Newtonsoft.Json.Linq.JToken.DynamicIsSupported", out bool jsonDynamic));
        Assert.IsFalse(jsonDynamic);
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
            var projectRoot = Microsoft.Build.Construction.ProjectRootElement.Create(
                XmlReader.Create(projectContent),
                projectCollection);
            var project = Microsoft.Build.Execution.ProjectInstance.FromProjectRootElement(
                projectRoot,
                new()
                {
                    ProjectCollection = projectCollection,
                    GlobalProperties = projectCollection.GlobalProperties,
                    EvaluationStage = ProjectEvaluationStage.Properties,
                });
            Assert.AreEqual("net11.0", project.GetPropertyValue("TargetFramework"));
            Assert.Contains(import =>
                import.EndsWith(
                    Path.Combine("Microsoft.NET.Sdk", "Sdk", "Sdk.targets"),
                    StringComparison.OrdinalIgnoreCase),
                project.ImportPaths);
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
