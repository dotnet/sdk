// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.NET.TestFramework;

namespace Microsoft.NET.Infrastructure.Tests;

/// <summary>
/// Tests for the MSBuild targets in ConditionalTests.targets that remove skipped test projects
/// from the Helix submission item group based on the SkippedTestScopes property.
/// </summary>
[TestClass]
public class RemoveSkippedConditionalTestProjectsTests : SdkTest
{
    private string _testProjPath = null!;
    private string _targetsRoot = null!;
    private string _dotnetPath = null!;

    [TestInitialize]
    public void TestInit()
    {
        // The ConditionalTestRemoval.proj asset is in TestAssets/TestProjects/
        _testProjPath = Path.Combine(TestAssetsManager.TestAssetsRoot, "TestProjects", "ConditionalTestRemoval", "ConditionalTestRemoval.proj");
        Assert.IsTrue(File.Exists(_testProjPath), $"Test project not found: {_testProjPath}");

        // Use the built SDK's dotnet (what Helix uses in CI)
        _dotnetPath = SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath;

        // ConditionalTests.targets lives under test/ in the repo root.
        // In Helix it's deployed to TestExecutionDirectory; locally use repo root.
        var testExecDir = SdkTestContext.Current.TestExecutionDirectory;
        var helixTargetsPath = Path.Combine(testExecDir, "test", "ConditionalTests.targets");
        if (File.Exists(helixTargetsPath))
        {
            _targetsRoot = testExecDir.TrimEnd(Path.DirectorySeparatorChar) + "/";
        }
        else
        {
            var repoRoot = SdkTestContext.Current.ToolsetUnderTest.RepoRoot
                ?? SdkTestContext.GetRepoRoot()
                ?? throw new InvalidOperationException("Could not determine repo root.");
            _targetsRoot = repoRoot.TrimEnd(Path.DirectorySeparatorChar) + "/";
        }
    }

    [TestMethod]
    public async Task EmptySkippedScopes_NothingRemoved()
    {
        using var env = new TestEnvironment(CreateSingleScopeProps());

        var remaining = await RunRemovalTarget(env, skippedScopes: "");

        // All projects should remain
        Assert.HasCount(3, remaining, string.Join(", ", remaining));
    }

    [TestMethod]
    public async Task SingleScopeSkipped_OnlyScopeProjectsRemoved()
    {
        using var env = new TestEnvironment(CreateTwoScopeProps());

        var remaining = await RunRemovalTarget(env, skippedScopes: "FeatureA");

        AssertNoneContain(remaining, "FeatureA", "FeatureA should be removed");
        AssertAnyContains(remaining, "FeatureB", "FeatureB should remain");
        AssertAnyContains(remaining, "Unrelated", "Unrelated should remain");
    }

    [TestMethod]
    public async Task MultipleScopesSkipped_AllMatchingScopesRemoved()
    {
        using var env = new TestEnvironment(CreateTwoScopeProps());

        var remaining = await RunRemovalTarget(env, skippedScopes: "FeatureA;FeatureB");

        AssertNoneContain(remaining, "FeatureA", "FeatureA should be removed");
        AssertNoneContain(remaining, "FeatureB", "FeatureB should be removed");
        AssertAnyContains(remaining, "Unrelated", "Unrelated should remain");
    }

    [TestMethod]
    public async Task AllKeyword_AllConditionalProjectsRemoved()
    {
        using var env = new TestEnvironment(CreateTwoScopeProps());

        var remaining = await RunRemovalTarget(env, skippedScopes: "__all__");

        AssertNoneContain(remaining, "FeatureA", "FeatureA should be removed");
        AssertNoneContain(remaining, "FeatureB", "FeatureB should be removed");
        AssertAnyContains(remaining, "Unrelated", "Unrelated should remain");
    }

    [TestMethod]
    public async Task MultiPathTestProjects_AllPathsRemovedForScope()
    {
        var props = """
            <Project>
              <PropertyGroup>
                <GlobalTriggerPaths>shared/**</GlobalTriggerPaths>
              </PropertyGroup>
              <ItemGroup>
                <ConditionalTestScope Include="MultiPath">
                  <Mechanism>project</Mechanism>
                  <TestProjects>test/PathA/**/*.csproj;test/PathB/**/*.csproj</TestProjects>
                  <TriggerPaths>src/**</TriggerPaths>
                  <RunAlways>CI</RunAlways>
                </ConditionalTestScope>
              </ItemGroup>
            </Project>
            """;

        using var env = new TestEnvironment(props, new[]
        {
            "test/PathA/A.Tests.csproj",
            "test/PathB/B.Tests.csproj",
            "test/Other/Other.Tests.csproj"
        });

        var remaining = await RunRemovalTarget(env, skippedScopes: "MultiPath");

        AssertNoneContain(remaining, "PathA", "PathA should be removed");
        AssertNoneContain(remaining, "PathB", "PathB should be removed");
        AssertAnyContains(remaining, "Other", "Other should remain");
    }

    [TestMethod]
    public async Task ScopeNotInSkippedList_ProjectsKept()
    {
        using var env = new TestEnvironment(CreateTwoScopeProps());

        var remaining = await RunRemovalTarget(env, skippedScopes: "FeatureA");

        AssertAnyContains(remaining, "FeatureB", "FeatureB should remain when not in SkippedTestScopes");
    }

    // --- Helpers ---

    private static string CreateSingleScopeProps() => """
        <Project>
          <PropertyGroup>
            <GlobalTriggerPaths>shared/**</GlobalTriggerPaths>
          </PropertyGroup>
          <ItemGroup>
            <ConditionalTestScope Include="TestScope">
              <Mechanism>project</Mechanism>
              <TestProjects>test/Feature.Tests/*.csproj</TestProjects>
              <TriggerPaths>src/Feature/**</TriggerPaths>
              <RunAlways>CI</RunAlways>
            </ConditionalTestScope>
          </ItemGroup>
        </Project>
        """;

    private static string CreateTwoScopeProps() => """
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

    private async Task<List<string>> RunRemovalTarget(TestEnvironment env, string skippedScopes)
    {
        // Write project paths to a file (one per line) since semicolons cannot reliably
        // pass through the MSBuild command-line parser.
        var itemsFile = Path.Combine(env.Root, "test-project-items.txt");
        File.WriteAllLines(itemsFile, env.TestProjectFiles);

        // Use forward slash to avoid the trailing-backslash-before-quote issue on Windows.
        var testRepoRoot = env.Root.TrimEnd(Path.DirectorySeparatorChar) + "/";

        // Escape semicolons in SkippedTestScopes with %3B for the command line.
        // ConditionalTestRemoval.proj unescapes them via $([MSBuild]::Unescape(...)).
        var escapedScopes = skippedScopes.Replace(";", "%3B");

        var args = $"msbuild \"{_testProjPath}\" /t:VerifyRemoval " +
                   $"/p:TestRepoRoot={testRepoRoot} " +
                   $"/p:RealRepoRoot={_targetsRoot} " +
                   $"/p:SkippedTestScopes={escapedScopes} " +
                   $"/p:TestProjectItemsFile={itemsFile} " +
                   $"/v:normal";

        var psi = new ProcessStartInfo(_dotnetPath, args)
        {
            WorkingDirectory = env.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var output = await Process.RunAndCaptureTextAsync(psi);

        Assert.AreEqual(0, output.ExitStatus.ExitCode, $"MSBuild failed.\nStdout: {output.StandardOutput}\nStderr: {output.StandardError}");

        // Parse REMAINING: lines from output
        return output.StandardOutput.Split('\n')
            .Where(line => line.Contains("REMAINING:"))
            .Select(line => line.Substring(line.IndexOf("REMAINING:") + "REMAINING:".Length).Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();
    }

    private static void AssertAnyContains(List<string> items, string substring, string message)
    {
        foreach (var item in items)
        {
            if (item.Contains(substring, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
        Assert.Fail($"{message}. None of [{string.Join(", ", items)}] contain '{substring}'.");
    }

    private static void AssertNoneContain(List<string> items, string substring, string message)
    {
        foreach (var item in items)
        {
            if (item.Contains(substring, StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail($"{message}. Found '{item}' containing '{substring}'.");
            }
        }
    }

    private sealed class TestEnvironment : IDisposable
    {
        public string Root { get; }
        public List<string> TestProjectFiles { get; } = new();

        private static readonly string[] s_defaultTestProjects =
        [
            "test/FeatureA.Tests/FeatureA.Tests.csproj",
            "test/FeatureB.Tests/FeatureB.Tests.csproj",
            "test/Unrelated.Tests/Unrelated.Tests.csproj"
        ];

        public TestEnvironment(string conditionalTestsPropsContent, string[]? testProjects = null)
        {
            Root = Path.Combine(Path.GetTempPath(), "infra-msbuild-tests-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(Root);

            // Write ConditionalTests.props
            var testDir = Path.Combine(Root, "test");
            Directory.CreateDirectory(testDir);
            File.WriteAllText(Path.Combine(testDir, "ConditionalTests.props"), conditionalTestsPropsContent);

            // Create dummy .csproj files so globs resolve
            foreach (var relativePath in testProjects ?? s_defaultTestProjects)
            {
                var fullPath = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, "<Project />");
                TestProjectFiles.Add(fullPath);
            }
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
