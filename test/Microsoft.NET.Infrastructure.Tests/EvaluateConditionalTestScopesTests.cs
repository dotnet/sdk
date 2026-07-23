// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.NET.TestFramework;

namespace Microsoft.NET.Infrastructure.Tests;

/// <summary>
/// End-to-end tests for scripts/EvaluateConditionalTestScopes.cs.
/// Each test creates a temporary git repo with a synthetic ConditionalTests.props,
/// invokes the script via `dotnet run`, and asserts on stdout/exit code.
/// </summary>
[TestClass]
public class EvaluateConditionalTestScopesTests : SdkTest
{
    private string _scriptPath = null!;
    private string _dotnetPath = null!;

    [TestInitialize]
    public void TestInit()
    {
        // Use the built SDK's dotnet (what Helix uses in CI)
        _dotnetPath = SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath;

        // Locate the script: in Helix it's deployed to TestExecutionDirectory;
        // locally use repo root.
        var testExecDir = SdkTestContext.Current.TestExecutionDirectory;
        var helixScriptPath = Path.Combine(testExecDir, "scripts", "EvaluateConditionalTestScopes.cs");
        if (File.Exists(helixScriptPath))
        {
            _scriptPath = helixScriptPath;
        }
        else
        {
            var repoRoot = SdkTestContext.Current.ToolsetUnderTest.RepoRoot
                ?? SdkTestContext.GetRepoRoot()
                ?? throw new InvalidOperationException("Could not determine repo root.");
            _scriptPath = Path.Combine(repoRoot, "scripts", "EvaluateConditionalTestScopes.cs");
        }

        Assert.IsTrue(File.Exists(_scriptPath), $"Script not found: {_scriptPath}");
    }

    [TestMethod]
    public async Task NoTargetBranch_AllScopesRun()
    {
        // When --target-branch is not provided, safe default: nothing skipped
        using var repo = new TestRepo(CreateBasicProps(), BasicPropsDirs);

        var result = await RunScript(repo.Root, targetBranch: null, buildReason: "PullRequest");

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        Assert.Contains("Scope 'TestScope': RUN", result.StdOut);
        Assert.Contains("Skipped test scopes: (none)", result.StdOut);
    }

    [TestMethod]
    public async Task CIBuild_RunAlwaysCI_AllScopesRun()
    {
        // Non-PR builds with RunAlways=CI → scopes always run
        using var repo = new TestRepo(CreateBasicProps(), BasicPropsDirs);
        repo.AddAndCommitFiles("main", "src/Unrelated/file.cs");
        repo.CreateBranchWithChanges("pr-branch", "docs/readme.md");

        var result = await RunScript(repo.Root, targetBranch: "main", buildReason: "IndividualCI");

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        Assert.Contains("Scope 'TestScope': RUN (RunAlways=CI)", result.StdOut);
    }

    [TestMethod]
    public async Task PRBuild_TriggerPathMatches_ScopeRuns()
    {
        // Changed file matches trigger path → scope runs
        using var repo = new TestRepo(CreateBasicProps(), BasicPropsDirs);
        repo.AddAndCommitFiles("main", "src/Unrelated/file.cs");
        repo.CreateBranchWithChanges("pr-branch", "src/MyFeature/Changed.cs");

        var result = await RunScript(repo.Root, targetBranch: "main", buildReason: "PullRequest");

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        Assert.Contains("Scope 'TestScope': RUN", result.StdOut);
        Assert.Contains("Skipped test scopes: (none)", result.StdOut);
    }

    [TestMethod]
    public async Task PRBuild_NoTriggerMatch_ScopeSkipped()
    {
        // Changed file does NOT match trigger path → scope skipped
        using var repo = new TestRepo(CreateBasicProps(), BasicPropsDirs);
        repo.AddAndCommitFiles("main", "src/Unrelated/file.cs");
        repo.CreateBranchWithChanges("pr-branch", "docs/unrelated.md");

        var result = await RunScript(repo.Root, targetBranch: "main", buildReason: "PullRequest");

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        Assert.Contains("Scope 'TestScope': SKIP", result.StdOut);
        Assert.Contains("Skipped test scopes: __all__", result.StdOut);
    }

    [TestMethod]
    public async Task PRBuild_GlobalTrigger_NothingSkipped()
    {
        // Changed file matches GlobalTriggerPaths → no scopes skipped
        using var repo = new TestRepo(CreateBasicProps(), BasicPropsDirs);
        repo.AddAndCommitFiles("main", "src/Unrelated/file.cs");
        repo.CreateBranchWithChanges("pr-branch", "test/Microsoft.NET.TestFramework/SomeHelper.cs");

        var result = await RunScript(repo.Root, targetBranch: "main", buildReason: "PullRequest");

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        Assert.Contains("Global trigger matched", result.StdOut);
        Assert.Contains("Skipped test scopes: (none)", result.StdOut);
    }

    [TestMethod]
    public async Task PRBuild_MultipleScopes_MixedResults()
    {
        // Two scopes: one matches, one doesn't
        var props = """
            <Project>
              <PropertyGroup>
                <GlobalTriggerPaths>shared/**</GlobalTriggerPaths>
              </PropertyGroup>
              <ItemGroup>
                <ConditionalTestScope Include="FeatureA">
                  <Mechanism>project</Mechanism>
                  <TestProjects>test/FeatureA.Tests/*.csproj</TestProjects>
                  <TriggerPaths>src/FeatureA/**</TriggerPaths>
                  <RunAlways>CI</RunAlways>
                </ConditionalTestScope>
                <ConditionalTestScope Include="FeatureB">
                  <Mechanism>project</Mechanism>
                  <TestProjects>test/FeatureB.Tests/*.csproj</TestProjects>
                  <TriggerPaths>src/FeatureB/**</TriggerPaths>
                  <RunAlways>CI</RunAlways>
                </ConditionalTestScope>
              </ItemGroup>
            </Project>
            """;

        using var repo = new TestRepo(props, "shared", "src/FeatureA", "src/FeatureB", "test/FeatureA.Tests", "test/FeatureB.Tests");
        repo.AddAndCommitFiles("main", "src/Unrelated/file.cs");
        // Only change files in FeatureA
        repo.CreateBranchWithChanges("pr-branch", "src/FeatureA/Code.cs");

        var result = await RunScript(repo.Root, targetBranch: "main", buildReason: "PullRequest");

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        Assert.Contains("Scope 'FeatureA': RUN", result.StdOut);
        Assert.Contains("Scope 'FeatureB': SKIP", result.StdOut);
        Assert.Contains("Skipped test scopes: FeatureB", result.StdOut);
    }

    [TestMethod]
    public async Task PRBuild_AllScopesSkipped_OutputsAll()
    {
        // When every scope is skipped, output is "__all__"
        using var repo = new TestRepo(CreateBasicProps(), BasicPropsDirs);
        repo.AddAndCommitFiles("main", "src/Unrelated/file.cs");
        repo.CreateBranchWithChanges("pr-branch", "completely/unrelated/path.txt");

        var result = await RunScript(repo.Root, targetBranch: "main", buildReason: "PullRequest");

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        Assert.Contains("Skipped test scopes: __all__", result.StdOut);
    }

    [TestMethod]
    public async Task PRBuild_MultipleScopesSkipped_PipelineVariableUsesPipeSeparator()
    {
        // Two of three scopes are skipped, so the pipeline variable lists both, separated by '|'
        // (see EvaluateConditionalTestScopes.cs for why '|' rather than ';').
        var props = """
            <Project>
              <PropertyGroup>
                <GlobalTriggerPaths>shared/**</GlobalTriggerPaths>
              </PropertyGroup>
              <ItemGroup>
                <ConditionalTestScope Include="FeatureA">
                  <Mechanism>project</Mechanism>
                  <TestProjects>test/FeatureA.Tests/*.csproj</TestProjects>
                  <TriggerPaths>src/FeatureA/**</TriggerPaths>
                  <RunAlways>CI</RunAlways>
                </ConditionalTestScope>
                <ConditionalTestScope Include="FeatureB">
                  <Mechanism>project</Mechanism>
                  <TestProjects>test/FeatureB.Tests/*.csproj</TestProjects>
                  <TriggerPaths>src/FeatureB/**</TriggerPaths>
                  <RunAlways>CI</RunAlways>
                </ConditionalTestScope>
                <ConditionalTestScope Include="FeatureC">
                  <Mechanism>project</Mechanism>
                  <TestProjects>test/FeatureC.Tests/*.csproj</TestProjects>
                  <TriggerPaths>src/FeatureC/**</TriggerPaths>
                  <RunAlways>CI</RunAlways>
                </ConditionalTestScope>
              </ItemGroup>
            </Project>
            """;

        using var repo = new TestRepo(props, "shared", "src/FeatureA", "src/FeatureB", "src/FeatureC",
            "test/FeatureA.Tests", "test/FeatureB.Tests", "test/FeatureC.Tests");
        repo.AddAndCommitFiles("main", "src/Unrelated/file.cs");
        // Only change files in FeatureC → FeatureA and FeatureB are skipped
        repo.CreateBranchWithChanges("pr-branch", "src/FeatureC/Code.cs");

        var result = await RunScript(repo.Root, targetBranch: "main", buildReason: "PullRequest", outputVariable: "SkippedTestScopes");

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        // The '|'-separated pipeline variable carries both skipped scopes.
        Assert.Contains("##vso[task.setvariable variable=SkippedTestScopes]FeatureA|FeatureB", result.StdOut);
        // The human-readable log line stays semicolon-separated for readability.
        Assert.Contains("Skipped test scopes: FeatureA;FeatureB", result.StdOut);
    }

    [TestMethod]
    public async Task GlobMatches_DoubleStarSlash_MatchesZeroSegments()
    {
        // Verify **/ pattern matches files directly in the parent (zero intermediate segments)
        var props = """
            <Project>
              <PropertyGroup>
                <GlobalTriggerPaths>never/**</GlobalTriggerPaths>
              </PropertyGroup>
              <ItemGroup>
                <ConditionalTestScope Include="GlobTest">
                  <Mechanism>project</Mechanism>
                  <TestProjects>test/Glob.Tests/*.csproj</TestProjects>
                  <TriggerPaths>src/**/File.cs</TriggerPaths>
                  <RunAlways>CI</RunAlways>
                </ConditionalTestScope>
              </ItemGroup>
            </Project>
            """;

        using var repo = new TestRepo(props, "never", "src", "test/Glob.Tests");
        repo.AddAndCommitFiles("main", "other/file.cs");
        // File directly under src/ (zero intermediate segments)
        repo.CreateBranchWithChanges("pr-branch", "src/File.cs");

        var result = await RunScript(repo.Root, targetBranch: "main", buildReason: "PullRequest");

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        Assert.Contains("Scope 'GlobTest': RUN", result.StdOut);
    }

    [TestMethod]
    public async Task Validation_MissingMechanism_ReturnsError()
    {
        var props = """
            <Project>
              <PropertyGroup>
                <GlobalTriggerPaths>shared/**</GlobalTriggerPaths>
              </PropertyGroup>
              <ItemGroup>
                <ConditionalTestScope Include="BadScope">
                  <TestProjects>test/*.csproj</TestProjects>
                  <TriggerPaths>src/**</TriggerPaths>
                  <RunAlways>CI</RunAlways>
                </ConditionalTestScope>
              </ItemGroup>
            </Project>
            """;

        using var repo = new TestRepo(props, "shared", "src");

        var result = await RunScript(repo.Root, targetBranch: null, buildReason: "PullRequest");

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.Contains("missing required <Mechanism> element", result.StdOut);
    }

    [TestMethod]
    public async Task Validation_UnrecognizedElement_ReturnsError()
    {
        var props = """
            <Project>
              <PropertyGroup>
                <GlobalTriggerPaths>shared/**</GlobalTriggerPaths>
              </PropertyGroup>
              <ItemGroup>
                <ConditionalTestScope Include="BadScope">
                  <Mechanism>project</Mechanism>
                  <TestProjects>test/*.csproj</TestProjects>
                  <TriggerPaths>src/**</TriggerPaths>
                  <RunAlways>CI</RunAlways>
                  <Typo>something</Typo>
                </ConditionalTestScope>
              </ItemGroup>
            </Project>
            """;

        using var repo = new TestRepo(props, "shared", "src");

        var result = await RunScript(repo.Root, targetBranch: null, buildReason: "PullRequest");

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.Contains("unrecognized element <Typo>", result.StdOut);
    }

    [TestMethod]
    public async Task Validation_MissingTriggerPaths_ReturnsError()
    {
        var props = """
            <Project>
              <ItemGroup>
                <ConditionalTestScope Include="BadScope">
                  <Mechanism>project</Mechanism>
                  <TestProjects>test/*.csproj</TestProjects>
                  <RunAlways>CI</RunAlways>
                </ConditionalTestScope>
              </ItemGroup>
            </Project>
            """;

        using var repo = new TestRepo(props);

        var result = await RunScript(repo.Root, targetBranch: null, buildReason: "PullRequest");

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.Contains("missing required <TriggerPaths> element", result.StdOut);
    }

    [TestMethod]
    public async Task Validation_MissingTestProjects_ReturnsError()
    {
        // When Mechanism=project, TestProjects is required
        var props = """
            <Project>
              <ItemGroup>
                <ConditionalTestScope Include="BadScope">
                  <Mechanism>project</Mechanism>
                  <TriggerPaths>src/**</TriggerPaths>
                  <RunAlways>CI</RunAlways>
                </ConditionalTestScope>
              </ItemGroup>
            </Project>
            """;

        using var repo = new TestRepo(props, "src");

        var result = await RunScript(repo.Root, targetBranch: null, buildReason: "PullRequest");

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.Contains("missing <TestProjects>", result.StdOut);
    }

    [TestMethod]
    public async Task Validation_TriggerPathBaseDir_DoesNotExist_ReturnsError()
    {
        var props = """
            <Project>
              <ItemGroup>
                <ConditionalTestScope Include="BadScope">
                  <Mechanism>project</Mechanism>
                  <TestProjects>test/*.csproj</TestProjects>
                  <TriggerPaths>nonexistent/path/**</TriggerPaths>
                  <RunAlways>CI</RunAlways>
                </ConditionalTestScope>
              </ItemGroup>
            </Project>
            """;

        // Deliberately do NOT create "nonexistent/path" directory
        using var repo = new TestRepo(props);

        var result = await RunScript(repo.Root, targetBranch: null, buildReason: "PullRequest");

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.Contains("base directory 'nonexistent/path' does not exist in the repo", result.StdOut);
    }

    [TestMethod]
    public async Task Validation_GlobalTriggerPathBaseDir_DoesNotExist_ReturnsError()
    {
        var props = """
            <Project>
              <PropertyGroup>
                <GlobalTriggerPaths>does-not-exist/**</GlobalTriggerPaths>
              </PropertyGroup>
              <ItemGroup>
                <ConditionalTestScope Include="MyScope">
                  <Mechanism>project</Mechanism>
                  <TestProjects>test/*.csproj</TestProjects>
                  <TriggerPaths>src/**</TriggerPaths>
                  <RunAlways>CI</RunAlways>
                </ConditionalTestScope>
              </ItemGroup>
            </Project>
            """;

        // Create src/ but NOT "does-not-exist/"
        using var repo = new TestRepo(props, "src");

        var result = await RunScript(repo.Root, targetBranch: null, buildReason: "PullRequest");

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.Contains("base directory 'does-not-exist' does not exist in the repo", result.StdOut);
    }

    [TestMethod]
    public async Task Validation_WildcardInDirectoryName_ValidatesParentSegment()
    {
        // A wildcard can appear within a path segment (e.g. "test/dotnet-format.*/**").
        // Validation should trim back to the last complete directory segment ("test")
        // rather than treating the partial segment ("test/dotnet-format.") as a directory.
        var props = """
            <Project>
              <ItemGroup>
                <ConditionalTestScope Include="WildcardDirName">
                  <Mechanism>project</Mechanism>
                  <TestProjects>test/*.csproj</TestProjects>
                  <TriggerPaths>test/dotnet-format.*/**</TriggerPaths>
                  <RunAlways>CI</RunAlways>
                </ConditionalTestScope>
              </ItemGroup>
            </Project>
            """;

        // The parent segment "test" exists, but no "test/dotnet-format." directory does.
        using var repo = new TestRepo(props, "test");

        var result = await RunScript(repo.Root, targetBranch: null, buildReason: "PullRequest");

        Assert.AreEqual(0, result.ExitCode, result.StdOut + result.StdErr);
    }

    [TestMethod]
    public async Task RenamedFile_OutOfTriggerPath_ScopeRuns()
    {
        // A file renamed OUT of a trigger path should still trigger that scope
        // (because --no-renames shows both old and new paths)
        using var repo = new TestRepo(CreateBasicProps(), BasicPropsDirs);
        repo.AddAndCommitFiles("main", "src/MyFeature/Original.cs");
        repo.CreateBranchWithRename("pr-branch", "src/MyFeature/Original.cs", "docs/Moved.cs");

        var result = await RunScript(repo.Root, targetBranch: "main", buildReason: "PullRequest");

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        // The old path (src/MyFeature/Original.cs) matches the trigger, so scope runs
        Assert.Contains("Scope 'TestScope': RUN", result.StdOut);
    }

    // --- Helpers ---

    private static string CreateBasicProps() => """
        <Project>
          <PropertyGroup>
            <GlobalTriggerPaths>
              test/Microsoft.NET.TestFramework/**
            </GlobalTriggerPaths>
          </PropertyGroup>
          <ItemGroup>
            <ConditionalTestScope Include="TestScope">
              <Mechanism>project</Mechanism>
              <TestProjects>test/MyFeature.Tests/*.csproj</TestProjects>
              <TriggerPaths>src/MyFeature/**;test/MyFeature.Tests/**</TriggerPaths>
              <RunAlways>CI</RunAlways>
            </ConditionalTestScope>
          </ItemGroup>
        </Project>
        """;

    // Directories that must exist for CreateBasicProps() path validation
    private static readonly string[] BasicPropsDirs =
        ["test/Microsoft.NET.TestFramework", "src/MyFeature", "test/MyFeature.Tests"];

    private async Task<ScriptResult> RunScript(string repoRoot, string? targetBranch, string buildReason, string? outputVariable = null)
    {
        var args = $"run \"{_scriptPath}\" -- --repo-root \"{repoRoot}\" --build-reason \"{buildReason}\"";
        if (targetBranch != null)
        {
            args += $" --target-branch \"{targetBranch}\"";
        }
        if (outputVariable != null)
        {
            args += $" --output-variable \"{outputVariable}\"";
        }

        var psi = new ProcessStartInfo(_dotnetPath, args)
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // The script only emits the ##vso[task.setvariable] line when TF_BUILD is set
        // (i.e. running under Azure Pipelines). Simulate that when an output variable is requested.
        if (outputVariable != null)
        {
            psi.Environment["TF_BUILD"] = "true";
        }

        // Remove test-framework env vars that interfere with child dotnet processes
        // (e.g., causing NETSDK1207 AOT errors). SdkTestContext.Initialize() sets these
        // for in-process MSBuild use, but they break out-of-process `dotnet run`.
        psi.Environment.Remove("MSBUILD_EXE_PATH");
        psi.Environment.Remove("MSBuildSDKsPath");
        psi.Environment.Remove("MSBuildExtensionsPath");

        // Ensure the built SDK is on PATH and DOTNET_ROOT points to it
        var dotnetRoot = Path.GetDirectoryName(_dotnetPath)!;
        psi.Environment["PATH"] = dotnetRoot + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        psi.Environment["DOTNET_ROOT"] = dotnetRoot;

        // Pass through NuGet cache so dotnet run can restore framework references from pre-built packages
        if (Environment.GetEnvironmentVariable("NUGET_PACKAGES") is string nugetPkgs)
        {
            psi.Environment["NUGET_PACKAGES"] = nugetPkgs;
        }

        var output = await Process.RunAndCaptureTextAsync(psi);

        // If the runtime is not available (e.g. local dev without build.cmd), skip gracefully
        if (output.StandardError.Contains("You must install or update .NET") || output.StandardOutput.Contains("You must install or update .NET"))
        {
            Assert.Inconclusive(
                "Required .NET runtime not available. Run build.cmd first or run these tests in CI. " +
                $"Details: {output.StandardError}");
        }

        return new ScriptResult(output.ExitStatus.ExitCode, output.StandardOutput, output.StandardError);
    }

    private record ScriptResult(int ExitCode, string StdOut, string StdErr);

    /// <summary>
    /// Creates a temporary git repository with a synthetic ConditionalTests.props.
    /// </summary>
    private sealed class TestRepo : IDisposable
    {
        public string Root { get; }

        public TestRepo(string conditionalTestsPropsContent, params string[] directories)
        {
            Root = Path.Combine(Path.GetTempPath(), "infra-tests-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(Root);

            // Create test/ConditionalTests.props
            var testDir = Path.Combine(Root, "test");
            Directory.CreateDirectory(testDir);
            File.WriteAllText(Path.Combine(testDir, "ConditionalTests.props"), conditionalTestsPropsContent);

            // Create any directories needed for path validation (TriggerPaths base dirs must exist)
            foreach (var dir in directories)
            {
                Directory.CreateDirectory(Path.Combine(Root, dir.Replace('/', Path.DirectorySeparatorChar)));
            }

            // Initialize git repo
            Git("init -b main");
            Git("config user.email \"test@test.com\"");
            Git("config user.name \"Test\"");

            // Initial commit so main has at least one commit
            File.WriteAllText(Path.Combine(Root, ".gitkeep"), "");
            Git("add .");
            Git("commit -m \"Initial commit\" --allow-empty");

            // Set up origin pointing to self so origin/<branch> refs work
            Git($"remote add origin \"{Root}\"");
        }

        /// <summary>
        /// Adds files on the specified branch and commits them.
        /// </summary>
        public void AddAndCommitFiles(string branch, params string[] filePaths)
        {
            Git($"checkout {branch}");
            foreach (var filePath in filePaths)
            {
                var fullPath = Path.Combine(Root, filePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, $"// {filePath}");
            }
            Git("add .");
            Git($"commit -m \"Add files on {branch}\"");

            // Update the origin ref so origin/<branch> resolves
            Git($"fetch origin");
        }

        /// <summary>
        /// Creates a new branch from main with specific changed files.
        /// </summary>
        public void CreateBranchWithChanges(string branchName, params string[] filePaths)
        {
            // Ensure origin/main is up to date before branching
            Git("update-ref refs/remotes/origin/main refs/heads/main");

            Git($"checkout -b {branchName}");
            foreach (var filePath in filePaths)
            {
                var fullPath = Path.Combine(Root, filePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, $"// {filePath} - changed");
            }
            Git("add .");
            Git($"commit -m \"Changes on {branchName}\"");
        }

        /// <summary>
        /// Creates a new branch from main with a file rename.
        /// </summary>
        public void CreateBranchWithRename(string branchName, string oldPath, string newPath)
        {
            // Ensure origin/main is up to date
            Git("update-ref refs/remotes/origin/main refs/heads/main");

            Git($"checkout -b {branchName}");
            var oldFull = Path.Combine(Root, oldPath.Replace('/', Path.DirectorySeparatorChar));
            var newFull = Path.Combine(Root, newPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(newFull)!);
            Git($"mv \"{oldFull}\" \"{newFull}\"");
            Git($"commit -m \"Rename {oldPath} to {newPath}\"");
        }

        private void Git(string args)
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi)!;
            process.WaitForExit(30_000);

            if (process.ExitCode != 0)
            {
                var stderr = process.StandardError.ReadToEnd();
                throw new InvalidOperationException($"git {args} failed (exit {process.ExitCode}): {stderr}");
            }
        }

        public void Dispose()
        {
            try
            {
                // Force-delete the temp directory (git objects are read-only)
                if (Directory.Exists(Root))
                {
                    foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup in tests
            }
        }
    }
}
