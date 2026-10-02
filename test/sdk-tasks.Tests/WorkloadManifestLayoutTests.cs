// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests;

/// <summary>
/// Verifies that manifest layout mappings must stay within owned paths.
/// </summary>
[TestClass]
public class WorkloadManifestLayoutTests : SdkTest
{
    /// <summary>
    /// Verifies escaping paths and destinations outside the recognized payload schema fail
    /// discovery instead of becoming authorized layout outputs.
    /// </summary>
    /// <param name="destination">The invalid destination relative to the fixture layout root.</param>
    [TestMethod]
    [DataRow("../outside/file.json")]
    [DataRow("backup/manifest/11.0.0/file.json")]
    [DataRow("11.0.100/manifest/backup/file.json")]
    [DataRow("11.0.100/workloadsets/11.0.100/custom.workloadset.json")]
    public void RejectsUnownedDestinations(string destination)
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        string layout = Path.Combine(root, "layout");
        var task = new GetWorkloadManifestLayout
        {
            LayoutRoot = layout,
            SourceFiles = [Input(CreateFile(root, "input"), Path.GetFullPath(Path.Combine(layout, destination)))],
            BuildEngine = new MockBuildEngine()
        };

        task.Execute().Should().BeFalse();
    }

    /// <summary>
    /// Verifies a staged payload cannot serve as an input that stale-output cleanup could delete.
    /// </summary>
    [TestMethod]
    public void RejectsSourcesInsideTheLayout()
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        var task = new GetWorkloadManifestLayout
        {
            LayoutRoot = root,
            SourceFiles = [Input(CreateFile(root, "11.0.100/old/11.0.0/file"), Path.Combine(root, "11.0.100", "new", "11.0.0", "file"))],
            BuildEngine = new MockBuildEngine()
        };

        task.Execute().Should().BeFalse();
    }

    /// <summary>
    /// Verifies directories whose names differ only in case remain distinct on case-sensitive
    /// file systems when identifying stale outputs.
    /// </summary>
    [TestMethod]
    public void TreatsCaseVariantDirectoriesAsDistinctOnCaseSensitiveFileSystems()
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        const string lowerBand = "11.0.100-alpha";
        const string upperBand = "11.0.100-ALPHA";
        string existing = CreateFile(root, Path.Combine(lowerBand, "test.manifest", "1.0.0", "WorkloadManifest.json"));
        Directory.CreateDirectory(Path.Combine(root, upperBand, "test.manifest", "1.0.0"));

        string[] bands = Directory.EnumerateDirectories(root).Select(Path.GetFileName).ToArray()!;
        if (!bands.Contains(lowerBand, StringComparer.Ordinal) || !bands.Contains(upperBand, StringComparer.Ordinal))
        {
            Assert.Inconclusive("The file system does not support distinct case-variant directories.");
        }

        string source = CreateFile(root, "input");
        var task = new GetWorkloadManifestLayout
        {
            LayoutRoot = root,
            SourceFiles = [Input(source, Path.Combine(root, upperBand, "test.manifest", "1.0.0", "WorkloadManifest.json"))],
            BuildEngine = new MockBuildEngine()
        };

        task.Execute().Should().BeTrue();
        task.StaleOutputs.Select(item => item.ItemSpec).Should().Contain(Path.GetFullPath(existing));
    }

    /// <summary>
    /// Verifies noncanonical feature-band and version directories are not considered layout-owned.
    /// </summary>
    /// <param name="relativePath">A file beneath a noncanonical directory name.</param>
    [TestMethod]
    [DataRow("9.0.100-rtm.24476/test.manifest/1.0.0/WorkloadManifest.json")]
    [DataRow("11.0.100/test.manifest/1.2.3-not valid/WorkloadManifest.json")]
    public void DoesNotOwnDirectoriesWithNoncanonicalNames(string relativePath)
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        string file = CreateFile(root, relativePath);
        string versionDirectory = Path.GetDirectoryName(file)!;
        var task = new GetWorkloadManifestLayout
        {
            LayoutRoot = root,
            BuildEngine = new MockBuildEngine()
        };

        task.Execute().Should().BeTrue();
        task.StaleOutputs.Select(item => item.ItemSpec).Should().NotContain(Path.GetFullPath(file));
        task.OwnedDirectories.Select(item => item.ItemSpec).Should().NotContain(Path.GetFullPath(versionDirectory));
    }

    /// <summary>
    /// Verifies canonical prerelease feature bands and four-part NuGet versions remain owned.
    /// </summary>
    [TestMethod]
    public void OwnsDirectoriesWithCanonicalPrereleaseAndFourPartVersions()
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        string file = CreateFile(root, "11.0.100-preview.2/test.manifest/1.2.3.4/WorkloadManifest.json");
        var task = new GetWorkloadManifestLayout
        {
            LayoutRoot = root,
            BuildEngine = new MockBuildEngine()
        };

        task.Execute().Should().BeTrue();
        task.StaleOutputs.Select(item => item.ItemSpec).Should().Contain(Path.GetFullPath(file));
    }

    [TestMethod]
    [DataRow("11.0.100.0")]
    [DataRow("11.0.100+build.7")]
    public void ResolvesNormalizedPackagePathsWithoutChangingManifestVersion(string version)
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        var manifest = new TaskItem("Test.Manifest");
        manifest.SetMetadata("Version", version);
        manifest.SetMetadata("NupkgId", "Test.Manifest-11.0.100");
        manifest.SetMetadata("MsiNupkgId", "Test.Manifest-11.0.100.Msi.x64");
        var task = new ResolveBundledManifestPackages
        {
            PackageRoot = root,
            Manifests = [manifest],
            BuildEngine = new MockBuildEngine()
        };

        task.Execute().Should().BeTrue();
        task.ResolvedManifests.Should().ContainSingle();
        task.ResolvedManifests[0].GetMetadata("Version").Should().Be(version);
        task.ResolvedManifests[0].GetMetadata("RestoredNupkgContentPath")
            .Should().Be(Path.Combine(root, "test.manifest-11.0.100", "11.0.100"));
        task.ResolvedManifests[0].GetMetadata("RestoredMsiNupkgContentPath")
            .Should().Be(Path.Combine(root, "test.manifest-11.0.100.msi.x64", "11.0.100"));
    }

    private static TaskItem Input(string source, string destination)
    {
        var item = new TaskItem(source);
        item.SetMetadata("DestinationPath", destination);
        return item;
    }

    private static string CreateFile(string root, string relativePath)
    {
        string path = Path.GetFullPath(Path.Combine(root, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "content");
        return path;
    }
}
