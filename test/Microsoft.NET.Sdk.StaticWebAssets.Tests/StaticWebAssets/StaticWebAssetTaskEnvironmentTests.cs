// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Runtime.InteropServices;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

// Test parallelization is disabled assembly-wide via
// [assembly:CollectionBehavior(DisableTestParallelization = true)] in
// LegacyStaticWebAssetsV1IntegrationTest.cs, which already isolates the
// process-CWD mutation these tests perform.
public class StaticWebAssetTaskEnvironmentTests
{
    [Fact]
    public void NormalizeContentRootPath_WithTaskEnvironment_AbsolutizesAgainstProjectDirectory_NotProcessCurrentDirectory()
    {
        WithDecoyCwdAndProjectDirectory((projectDir, _) =>
        {
            var env = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir);

            var result = StaticWebAsset.NormalizeContentRootPath("wwwroot", env);

            result.Should().Be(Path.Combine(projectDir, "wwwroot") + Path.DirectorySeparatorChar,
                "the relative ContentRoot must be resolved against TaskEnvironment.ProjectDirectory, not the process CWD");
        });
    }

    [Fact]
    public void NormalizeContentRootPath_WithoutEnvOverload_StillUsesProcessCurrentDirectory_ForBackCompat()
    {
        WithDecoyCwdAndProjectDirectory((_, spawnDir) =>
        {
            // The parameterless overload preserves the pre-existing behavior so unmigrated
            // call sites continue to work; only callers that opt in via the env overload get
            // the MT-safe resolution.
            var result = StaticWebAsset.NormalizeContentRootPath("wwwroot");

            result.Should().Be(Path.Combine(spawnDir, "wwwroot") + Path.DirectorySeparatorChar);
        });
    }

    [Fact]
    public void Normalize_WithTaskEnvironment_AbsolutizesContentRootAndRelatedAssetAgainstProjectDirectory()
    {
        WithDecoyCwdAndProjectDirectory((projectDir, _) =>
        {
            var env = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir);
            var asset = new StaticWebAsset
            {
                Identity = Path.Combine(projectDir, "site.css"),
                SourceId = "MyProject",
                SourceType = StaticWebAsset.SourceTypes.Discovered,
                ContentRoot = "wwwroot",
                BasePath = "/",
                RelativePath = "site.css",
                RelatedAsset = "related/asset.css",
            };

            asset.Normalize(env);

            asset.ContentRoot.Should().Be(Path.Combine(projectDir, "wwwroot") + Path.DirectorySeparatorChar);
            asset.RelatedAsset.Should().Be(Path.Combine(projectDir, "related", "asset.css"));
        });
    }

    [Fact]
    public void FromTaskItem_WithTaskEnvironment_HydratesAssetWithProjectDirectoryAbsolutizedPaths()
    {
        WithDecoyCwdAndProjectDirectory((projectDir, _) =>
        {
            var env = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir);
            var item = new TaskItem(Path.Combine(projectDir, "site.css"), new Dictionary<string, string>
            {
                [nameof(StaticWebAsset.SourceId)] = "MyProject",
                [nameof(StaticWebAsset.SourceType)] = StaticWebAsset.SourceTypes.Discovered,
                [nameof(StaticWebAsset.ContentRoot)] = "wwwroot",
                [nameof(StaticWebAsset.BasePath)] = "/",
                [nameof(StaticWebAsset.RelativePath)] = "site.css",
                [nameof(StaticWebAsset.RelatedAsset)] = "",
            });

            var asset = StaticWebAsset.FromTaskItem(item, env);

            asset.ContentRoot.Should().Be(Path.Combine(projectDir, "wwwroot") + Path.DirectorySeparatorChar);
        });
    }

    [Fact]
    public void FromV1TaskItem_WithTaskEnvironment_HydratesAssetWithProjectDirectoryAbsolutizedPaths()
    {
        WithDecoyCwdAndProjectDirectory((projectDir, _) =>
        {
            var env = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir);
            var assetIdentity = Path.Combine(projectDir, "wwwroot", "site.css");
            Directory.CreateDirectory(Path.GetDirectoryName(assetIdentity));
            File.WriteAllText(assetIdentity, "body{}");
            var item = new TaskItem(assetIdentity, new Dictionary<string, string>
            {
                [nameof(StaticWebAsset.SourceId)] = "SomePackage",
                [nameof(StaticWebAsset.SourceType)] = StaticWebAsset.SourceTypes.Package,
                [nameof(StaticWebAsset.ContentRoot)] = "wwwroot",
                [nameof(StaticWebAsset.BasePath)] = "_content/SomePackage",
                [nameof(StaticWebAsset.RelativePath)] = "site.css",
                [nameof(StaticWebAsset.OriginalItemSpec)] = assetIdentity,
                [nameof(StaticWebAsset.Fingerprint)] = "deadbeef",
                [nameof(StaticWebAsset.Integrity)] = "sha256-fake",
                [nameof(StaticWebAsset.FileLength)] = "6",
                [nameof(StaticWebAsset.LastWriteTime)] = DateTimeOffset.UtcNow.ToString("o"),
            });

            var asset = StaticWebAsset.FromV1TaskItem(item, env);

            asset.ContentRoot.Should().Be(Path.Combine(projectDir, "wwwroot") + Path.DirectorySeparatorChar);
        });
    }

    [Fact]
    public void FromTaskItemGroup_WithTaskEnvironment_AbsolutizesAllAssets()
    {
        WithDecoyCwdAndProjectDirectory((projectDir, _) =>
        {
            var env = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir);
            var items = new ITaskItem[]
            {
                MakeDiscoveredItem(projectDir, contentRoot: "wwwroot", relativePath: "a.css"),
                MakeDiscoveredItem(projectDir, contentRoot: "wwwroot", relativePath: "b.css"),
            };

            var assets = StaticWebAsset.FromTaskItemGroup(items, env);

            assets.Should().AllSatisfy(asset =>
                asset.ContentRoot.Should().Be(Path.Combine(projectDir, "wwwroot") + Path.DirectorySeparatorChar));
        });
    }

    [Fact]
    public void ResolveFile_WithTaskEnvironment_ResolvesIdentityRelativeToProjectDirectory()
    {
        WithDecoyCwdAndProjectDirectory((projectDir, _) =>
        {
            var realFile = Path.Combine(projectDir, "wwwroot", "site.css");
            Directory.CreateDirectory(Path.GetDirectoryName(realFile));
            File.WriteAllText(realFile, "body{}");

            var env = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir);

            var info = StaticWebAsset.ResolveFile(Path.Combine("wwwroot", "site.css"), originalItemSpec: "", env);

            info.FullName.Should().Be(realFile);
            info.Exists.Should().BeTrue();
        });
    }

    [Fact]
    public void HasContentRoot_WithTaskEnvironment_ComparesAgainstProjectDirectoryNormalizedForm()
    {
        WithDecoyCwdAndProjectDirectory((projectDir, _) =>
        {
            var env = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir);
            var asset = new StaticWebAsset
            {
                Identity = Path.Combine(projectDir, "site.css"),
                ContentRoot = "wwwroot",
                BasePath = "/",
                RelativePath = "site.css",
                SourceId = "X",
                SourceType = StaticWebAsset.SourceTypes.Discovered,
            };
            asset.Normalize(env);

            asset.HasContentRoot("wwwroot", env).Should().BeTrue();
        });
    }

    [Fact]
    public void NormalizeContentRootPath_WithTaskEnvironment_PreservesCanonicalization_DotDot()
    {
        WithDecoyCwdAndProjectDirectory((projectDir, _) =>
        {
            var env = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir);

            var result = StaticWebAsset.NormalizeContentRootPath(Path.Combine("a", "..", "wwwroot"), env);

            // Outer Path.GetFullPath collapses ".." segments while the inner env.GetAbsolutePath
            // ensures the base is the project directory. The two together preserve the
            // pre-migration canonicalization semantics callers depend on for equality checks.
            result.Should().Be(Path.Combine(projectDir, "wwwroot") + Path.DirectorySeparatorChar);
        });
    }

    [Fact]
    public void Normalize_WithTaskEnvironment_AbsolutePathInputs_ArePreservedAndCanonicalized()
    {
        // The single most common production case: upstream targets pre-absolutize. The new
        // overload must remain a no-op for already-absolute inputs (no surprise re-rooting).
        WithDecoyCwdAndProjectDirectory((projectDir, _) =>
        {
            var env = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir);
            var absoluteContentRoot = Path.Combine(projectDir, "alreadyabsolute");
            var absoluteRelatedAsset = Path.Combine(projectDir, "other", "asset.css");
            var asset = new StaticWebAsset
            {
                Identity = Path.Combine(projectDir, "site.css"),
                SourceId = "X",
                SourceType = StaticWebAsset.SourceTypes.Discovered,
                ContentRoot = absoluteContentRoot,
                BasePath = "/",
                RelativePath = "site.css",
                RelatedAsset = absoluteRelatedAsset,
            };

            asset.Normalize(env);

            asset.ContentRoot.Should().Be(absoluteContentRoot + Path.DirectorySeparatorChar);
            asset.RelatedAsset.Should().Be(absoluteRelatedAsset);
        });
    }

    [Fact]
    public void NormalizeContentRootPath_WithWhitespacePath_PreservesLegacyBehavior_DoesNotResolveAgainstProjectDirectory()
    {
        WithDecoyCwdAndProjectDirectory((projectDir, spawnDir) =>
        {
            var env = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir);
            const string whitespace = "   ";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Before the multithreading migration the raw whitespace path flowed straight into
                // Path.GetFullPath, which on Windows trims the trailing whitespace to an empty path
                // and throws. Routing it through env.GetAbsolutePath first would instead silently
                // resolve it to the project directory, masking the invalid input. Guarding on
                // IsNullOrWhiteSpace preserves the original throw.
                Action act = () => StaticWebAsset.NormalizeContentRootPath(whitespace, env);

                act.Should().Throw<ArgumentException>(
                    "a whitespace-only path is invalid on Windows and must keep throwing as it did before the migration");
            }
            else
            {
                // On Unix whitespace is a legal path segment, so Path.GetFullPath does not throw.
                // It must still resolve against the legacy base (the process CWD) exactly like the
                // parameterless overload, rather than being silently re-rooted to the project directory.
                var result = StaticWebAsset.NormalizeContentRootPath(whitespace, env);

                result.Should().Be(StaticWebAsset.NormalizeContentRootPath(whitespace),
                    "whitespace paths must keep their pre-migration resolution and not be re-rooted to the project directory");
                result.Should().StartWith(spawnDir);
                result.Should().NotStartWith(projectDir);
            }
        });
    }

    private static ITaskItem MakeDiscoveredItem(string projectDir, string contentRoot, string relativePath)
    {
        return new TaskItem(Path.Combine(projectDir, "site.css"), new Dictionary<string, string>
        {
            [nameof(StaticWebAsset.SourceId)] = "MyProject",
            [nameof(StaticWebAsset.SourceType)] = StaticWebAsset.SourceTypes.Discovered,
            [nameof(StaticWebAsset.ContentRoot)] = contentRoot,
            [nameof(StaticWebAsset.BasePath)] = "/",
            [nameof(StaticWebAsset.RelativePath)] = relativePath,
        });
    }

    private static void WithDecoyCwdAndProjectDirectory(Action<string, string> body)
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(StaticWebAssetTaskEnvironmentTests), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "decoy", "spawn");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(spawnDir);

        var originalCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);
            body(projectDir, spawnDir);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }
}
