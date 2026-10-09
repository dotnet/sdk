// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Build.Tasks;

/// <summary>
/// Discovers only feature-band userlocal markers owned by the source-only layout.
/// </summary>
public sealed class GetWorkloadUserLocalLayout : Task
{
    [Required]
    public string LayoutRoot { get; set; } = string.Empty;

    public string FeatureBand { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    [Output]
    public ITaskItem[] ExpectedOutputs { get; private set; } = [];

    [Output]
    public ITaskItem[] StaleOutputs { get; private set; } = [];

    [Output]
    public ITaskItem[] OwnedDirectories { get; private set; } = [];

    [Output]
    public bool NeedsRepair { get; private set; }

    public override bool Execute()
    {
        try
        {
            if (!IncrementalLayoutState.TryGetFullPath(Log, LayoutRoot, nameof(LayoutRoot), out string root))
            {
                return false;
            }

            if (Enabled && !GetWorkloadManifestLayout.IsFeatureBand(FeatureBand))
            {
                Log.LogError($"Invalid workload marker feature band '{FeatureBand}'.");
                return false;
            }

            string workloads = Path.Combine(root, "metadata", "workloads");

            string? expected = Enabled ? Path.Combine(workloads, FeatureBand, "userlocal") : null;
            var stale = new List<ITaskItem>();
            var directories = new List<ITaskItem>();
            if (Directory.Exists(workloads))
            {
                foreach (string directory in Directory.EnumerateDirectories(workloads))
                {
                    if (!GetWorkloadManifestLayout.IsFeatureBand(Path.GetFileName(directory)))
                    {
                        continue;
                    }

                    string marker = Path.Combine(directory, "userlocal");
                    LayoutPathValidation.RejectDirectory(marker);
                    directories.Add(new TaskItem(directory));
                    NeedsRepair |= !Directory.EnumerateFileSystemEntries(directory).Any();
                    if (File.Exists(marker) && !IncrementalLayoutState.PathComparer.Equals(marker, expected))
                    {
                        stale.Add(new TaskItem(marker));
                    }
                }
            }

            ExpectedOutputs = expected is null ? [] : [new TaskItem(expected)];
            NeedsRepair |= expected is not null && File.Exists(expected) && new FileInfo(expected).Length != 0;
            StaleOutputs = stale.ToArray();
            OwnedDirectories = directories.ToArray();
        }
        catch (Exception exception)
        {
            Log.LogErrorFromException(exception, showStackTrace: true);
        }

        return !Log.HasLoggedErrors;
    }
}
