// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.NET.Infrastructure.Tests;

[TestClass]
public class RunTestsScriptTests : SdkTest
{
    private string _dotnetPath = null!;
    private string _repoRoot = null!;
    private string _scriptPath = null!;

    [TestInitialize]
    public void TestInit()
    {
        _dotnetPath = SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath;

        string? repoRoot = SdkTestContext.Current.ToolsetUnderTest.RepoRoot
            ?? SdkTestContext.GetRepoRoot();
        if (repoRoot is null
            || (!Directory.Exists(Path.Combine(repoRoot, ".git"))
                && !File.Exists(Path.Combine(repoRoot, ".git"))))
        {
            Assert.Inconclusive("run-tests is a local-checkout tool and is not deployed to Helix.");
            return;
        }

        _repoRoot = repoRoot;
        _scriptPath = Path.Combine(
            _repoRoot,
            "scripts",
            "RunTests.cs");

        Assert.IsTrue(File.Exists(_scriptPath), $"Script not found: {_scriptPath}");
    }

    [TestMethod]
    public async Task HelpDescribesTheConsolidatedRunner()
    {
        ScriptResult result = await RunScript("--help");

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        Assert.Contains("Run one dotnet/sdk test project", result.StdOut);
        Assert.Contains("--project", result.StdOut);
        Assert.Contains("--filter", result.StdOut);
        Assert.Contains("--framework", result.StdOut);
        Assert.Contains("--skip-redist-check", result.StdOut);
        Assert.DoesNotContain("--no-build", result.StdOut);
    }

    [TestMethod]
    public async Task MissingProjectReturnsAClearError()
    {
        ScriptResult result = await RunScript("--project", "test/Missing.Tests.csproj");

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.Contains("Test project does not exist", result.StdErr);
    }

    [TestMethod]
    public async Task UnsupportedConfigurationReturnsAParserError()
    {
        ScriptResult result = await RunScript(
            "--project",
            "test/Missing.Tests.csproj",
            "--configuration",
            "Checked");

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.Contains("Unsupported configuration 'Checked'. Use Debug or Release.", result.StdErr);
    }

    [TestMethod]
    public async Task RunsSingleTargetMtpProjectWithDiagnostics()
    {
        ScriptResult result = await RunScript(
            "--project",
            "test/Microsoft.DotNet.Cli.Utils.Tests/Microsoft.DotNet.Cli.Utils.Tests.csproj",
            "--filter",
            "FullyQualifiedName~ArgumentEscaperTests.EscapesArgumentsForProcessStart",
            "--skip-redist-check");

        Assert.AreEqual(0, result.ExitCode, result.StdOut + result.StdErr);
        Assert.Contains($"Framework: {ToolsetInfo.CurrentTargetFramework}", result.StdOut);
        Assert.Contains("TRX: artifacts", result.StdOut);
        Assert.Contains("Binlog: artifacts", result.StdOut);
    }

    [TestMethod]
    public async Task MultiTargetedProjectDefaultsToSdkTargetFramework()
    {
        ScriptResult result = await RunWorkloadManifestReaderTests();

        Assert.AreEqual(0, result.ExitCode, result.StdOut + result.StdErr);
        Assert.Contains($"Framework: {ToolsetInfo.CurrentTargetFramework}", result.StdOut);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task MultiTargetedProjectRunsNetFrameworkExecutable()
    {
        ScriptResult result = await RunWorkloadManifestReaderTests("net472");

        Assert.AreEqual(0, result.ExitCode, result.StdOut + result.StdErr);
        Assert.Contains("Framework: net472", result.StdOut);
        Assert.Contains(
            @"Command: .\artifacts\bin\Microsoft.NET.Sdk.WorkloadManifestReader.Tests\Debug\net472\Microsoft.NET.Sdk.WorkloadManifestReader.Tests.exe",
            result.StdOut);
    }

    [TestMethod]
    public async Task InvalidFrameworkReturnsAProjectSpecificErrorAndPortableRerun()
    {
        ScriptResult result = await RunWorkloadManifestReaderTests("net999.0");

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.Contains("Target framework 'net999.0' is not listed in TargetFrameworks", result.StdErr);
        Assert.Contains("Rerun:", result.StdErr);
        Assert.Contains(Path.Combine("scripts", "RunTests.cs"), result.StdErr);
        Assert.DoesNotContain(_repoRoot, result.StdErr);
    }

    private Task<ScriptResult> RunWorkloadManifestReaderTests(string? framework = null)
    {
        var arguments = new List<string>
        {
            "--project",
            "test/Microsoft.NET.Sdk.WorkloadManifestReader.Tests/Microsoft.NET.Sdk.WorkloadManifestReader.Tests.csproj",
            "--filter",
            "FullyQualifiedName~ManifestReaderTests.SdkFeatureBandTests.ItParsesVersionsCorrectly",
            "--skip-redist-check"
        };
        if (framework is not null)
        {
            arguments.Add("--framework");
            arguments.Add(framework);
        }

        return RunScript([.. arguments]);
    }

    private async Task<ScriptResult> RunScript(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(_dotnetPath)
        {
            WorkingDirectory = _repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(_scriptPath);
        startInfo.ArgumentList.Add("--");
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment.Remove("MSBUILD_EXE_PATH");
        startInfo.Environment.Remove("MSBuildSDKsPath");
        startInfo.Environment.Remove("MSBuildExtensionsPath");

        var output = await Process.RunAndCaptureTextAsync(startInfo);
        return new ScriptResult(
            output.ExitStatus.ExitCode,
            output.StandardOutput,
            output.StandardError);
    }

    private sealed record ScriptResult(int ExitCode, string StdOut, string StdErr);
}
