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
