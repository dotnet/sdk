// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests;

/// <summary>
/// Verifies manifest ownership boundaries, stable mappings, and safe filesystem discovery and pruning.
/// </summary>
[TestClass]
public class WorkloadManifestLayoutTests : SdkTest
{
    /// <summary>
    /// Verifies schema-based discovery finds retired and nested payloads without cached ownership,
    /// while excluding malformed paths and unrelated workload-set files.
    /// </summary>
    [TestMethod]
    public void DiscoversRetiredPayloadsWithoutAnInventory()
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        string[] owned =
        [
            CreateFile(root, "10.0.100/retired.manifest/10.0.1/WorkloadManifest.json"),
            CreateFile(root, "11.0.100-preview.1/current.manifest/11.0.0-preview.1/localize/WorkloadManifest.en.json"),
            CreateFile(root, "11.0.100/current.manifest/11.0.0/extra/nested/file.props"),
            CreateFile(root, "11.0.100/manifest/36.0.1.2/WorkloadManifest.json"),
            CreateFile(root, "11.0.100/workloadsets/11.0.100/baseline.workloadset.json")
        ];
        string[] unowned =
        [
            CreateFile(root, "keep.txt"),
            CreateFile(root, "backup/retired.manifest/10.0.1/WorkloadManifest.json"),
            CreateFile(root, "11.0.101/manifest/11.0.0/WorkloadManifest.json"),
            CreateFile(root, "11.0.100/current.manifest/backup/WorkloadManifest.json"),
            CreateFile(root, "11.0.100/current.manifest/keep.txt"),
            CreateFile(root, "11.0.100/workloadsets/11.0.100/custom.workloadset.json"),
            CreateFile(root, "11.0.100/workloadsets/11.0.100/nested/baseline.workloadset.json")
        ];
        var task = new GetWorkloadManifestLayout { LayoutRoot = root, BuildEngine = new MockBuildEngine() };

        task.Execute().Should().BeTrue();
        task.ExistingOutputs.Select(item => item.ItemSpec).Should().BeEquivalentTo(owned);
        task.ExistingOutputs.Select(item => item.ItemSpec).Should().NotIntersectWith(unowned);
    }

    /// <summary>
    /// Verifies stale-file comparison honors the platform path policy without assuming that
    /// non-Windows storage can represent distinct case-only filenames.
    /// </summary>
    /// <param name="fileName">An unrelated filename or a case variant of the expected payload.</param>
    [TestMethod]
    [DataRow("retired.json")]
    [DataRow("WORKLOADMANIFEST.JSON")]
    public void StaleOutputsUsePlatformPathComparison(string fileName)
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        string layout = Path.Combine(root, "layout");
        string expected = CreateFile(layout, "11.0.100/manifest/11.0.0/WorkloadManifest.json");
        string extra = Path.Combine(Path.GetDirectoryName(expected)!, fileName);
        // Probe this directory: macOS volumes, for example, can use either case policy.
        bool aliasesExpected = File.Exists(extra);
        if (aliasesExpected && !OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Distinct case-only payloads require a case-sensitive filesystem.");
        }
        if (!aliasesExpected)
        {
            CreateFile(layout, $"11.0.100/manifest/11.0.0/{fileName}");
        }
        var discovery = new GetWorkloadManifestLayout
        {
            LayoutRoot = layout,
            SourceFiles = [Input(CreateFile(root, "input"), aliasesExpected ? extra : expected)],
            BuildEngine = new MockBuildEngine()
        };

        discovery.Execute().Should().BeTrue();
        if (aliasesExpected || (OperatingSystem.IsWindows() && fileName == "WORKLOADMANIFEST.JSON"))
        {
            discovery.StaleOutputs.Should().BeEmpty();
        }
        else
        {
            discovery.StaleOutputs.Select(item => item.ItemSpec).Should().Equal(extra);
        }
    }

    /// <summary>
    /// Verifies reordering producer inputs does not change the serialized mapping inventory
    /// or destination-sorted source items.
    /// </summary>
    [TestMethod]
    public void CanonicalMappingsAreIndependentOfInputOrder()
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        string layout = Path.Combine(root, "layout");
        TaskItem first = Input(CreateFile(root, "inputs/first"), Path.Combine(layout, "11.0.100", "manifest", "11.0.0", "first"));
        TaskItem second = Input(CreateFile(root, "inputs/second"), Path.Combine(layout, "11.0.100", "manifest", "11.0.0", "second"));
        var task = new GetWorkloadManifestLayout { LayoutRoot = layout, SourceFiles = [first, second], BuildEngine = new MockBuildEngine() };
        var reordered = new GetWorkloadManifestLayout { LayoutRoot = layout, SourceFiles = [second, first], BuildEngine = new MockBuildEngine() };

        task.Execute().Should().BeTrue();
        reordered.Execute().Should().BeTrue();
        reordered.InputManifestLines.Select(item => item.ItemSpec).Should().Equal(task.InputManifestLines.Select(item => item.ItemSpec));
        reordered.SourceFilesWithDestinations.Select(item => item.ItemSpec).Should().Equal(first.ItemSpec, second.ItemSpec);
    }

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
        task.SourceFilesWithDestinations.Should().BeEmpty();
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
    /// Verifies pruning removes an empty owned subtree while preserving the layout root
    /// and an unrelated file in its feature-band directory.
    /// </summary>
    [TestMethod]
    public void PrunesOnlyEmptyDescendants()
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        string empty = Path.Combine(root, "11.0.100", "retired", "11.0.0", "localize");
        Directory.CreateDirectory(empty);
        string unowned = CreateFile(root, "11.0.100/keep.txt");
        var discovery = new GetWorkloadManifestLayout { LayoutRoot = root, BuildEngine = new MockBuildEngine() };
        discovery.Execute().Should().BeTrue();
        discovery.EmptyDirectories.Select(item => item.ItemSpec).Should().Contain(empty);

        var prune = new PruneEmptyLayoutDirectories
        {
            LayoutRoot = root,
            Directories = discovery.OwnedDirectories,
            BuildEngine = new MockBuildEngine()
        };
        prune.Execute().Should().BeTrue();
        Directory.Exists(Path.Combine(root, "11.0.100", "retired")).Should().BeFalse();
        File.Exists(unowned).Should().BeTrue();
        Directory.Exists(root).Should().BeTrue();
    }

    /// <summary>
    /// Verifies invalid pruning candidates prevent deletion even when valid empty directories
    /// appear earlier in the candidate list.
    /// </summary>
    [TestMethod]
    public void PruningRejectsRootAndEscapingPathsBeforeMutation()
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        string empty = Path.Combine(root, "empty");
        Directory.CreateDirectory(empty);
        foreach (string invalid in new[] { root, Path.GetDirectoryName(root)! })
        {
            var task = new PruneEmptyLayoutDirectories
            {
                LayoutRoot = root,
                Directories = [new TaskItem(empty), new TaskItem(invalid)],
                BuildEngine = new MockBuildEngine()
            };
            task.Execute().Should().BeFalse();
            Directory.Exists(empty).Should().BeTrue();
        }
    }

    /// <summary>
    /// Verifies discovery and pruning reject a linked payload directory without touching
    /// the external file it exposes.
    /// </summary>
    [TestMethod]
    public void RejectsLinkedPayloadDirectories()
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        string layout = Path.Combine(root, "layout");
        string external = Path.Combine(root, "external");
        string externalFile = CreateFile(root, "external/WorkloadManifest.json");
        string link = Path.Combine(layout, "11.0.100", "manifest", "11.0.0");
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        Directory.CreateSymbolicLink(link, external);

        try
        {
            var discovery = new GetWorkloadManifestLayout { LayoutRoot = layout, BuildEngine = new MockBuildEngine() };
            discovery.Execute().Should().BeFalse();
            var prune = new PruneEmptyLayoutDirectories
            {
                LayoutRoot = layout,
                Directories = [new TaskItem(link)],
                BuildEngine = new MockBuildEngine()
            };
            prune.Execute().Should().BeFalse();
            File.Exists(externalFile).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    /// <summary>
    /// Verifies reusing a discovery task cannot reuse successful link checks from an earlier
    /// execution after a payload path is replaced with a symbolic link.
    /// </summary>
    /// <param name="linkFile">Whether to replace the payload file rather than its version directory.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DiscoveryRechecksLinksOnEveryExecution(bool linkFile)
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        string layout = Path.Combine(root, "layout");
        string payload = CreateFile(layout, "11.0.100/manifest/11.0.0/WorkloadManifest.json");
        string externalFile = CreateFile(root, "external/WorkloadManifest.json");
        var discovery = new GetWorkloadManifestLayout
        {
            LayoutRoot = layout,
            SourceFiles = [Input(externalFile, payload)],
            BuildEngine = new MockBuildEngine()
        };
        discovery.Execute().Should().BeTrue();
        File.Delete(payload);
        string link = linkFile ? payload : Path.GetDirectoryName(payload)!;
        if (linkFile)
        {
            File.CreateSymbolicLink(link, externalFile);
        }
        else
        {
            Directory.Delete(link);
            Directory.CreateSymbolicLink(link, Path.GetDirectoryName(externalFile)!);
        }

        try
        {
            discovery.Execute().Should().BeFalse();
            File.ReadAllText(externalFile).Should().Be("content");
        }
        finally
        {
            if (linkFile)
            {
                File.Delete(link);
            }
            else
            {
                Directory.Delete(link);
            }
        }
    }

    /// <summary>
    /// Verifies caching a validated payload's ancestors does not hide a linked sibling
    /// encountered later during ownership discovery.
    /// </summary>
    [TestMethod]
    public void DiscoveryRejectsLinkedSiblingsOfValidatedPaths()
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        string layout = Path.Combine(root, "layout");
        string payload = CreateFile(layout, "11.0.100/manifest/11.0.0/WorkloadManifest.json");
        string externalFile = CreateFile(root, "external/WorkloadManifest.json");
        string link = Path.Combine(Path.GetDirectoryName(payload)!, "linked");
        Directory.CreateSymbolicLink(link, Path.GetDirectoryName(externalFile)!);

        try
        {
            var discovery = new GetWorkloadManifestLayout
            {
                LayoutRoot = layout,
                SourceFiles = [Input(externalFile, payload)],
                BuildEngine = new MockBuildEngine()
            };
            discovery.Execute().Should().BeFalse();
            File.ReadAllText(externalFile).Should().Be("content");
        }
        finally
        {
            Directory.Delete(link);
        }
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
