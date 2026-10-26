// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Build.Tasks;

/// <summary>
/// Discovers the two owned root launcher paths and detects Unix permission changes.
/// </summary>
public sealed class GetDnxShimLayout : Task
{
    [Required]
    public string LayoutRoot { get; set; } = string.Empty;

    public ITaskItem[] SourceFiles { get; set; } = [];

    [Output]
    public ITaskItem[] ExpectedOutputs { get; private set; } = [];

    [Output]
    public ITaskItem[] StaleOutputs { get; private set; } = [];

    [Output]
    public bool PermissionsNeedRepair { get; private set; }

    [Output]
    public bool ContentsNeedRepair { get; private set; }

    public override bool Execute()
    {
        try
        {
            if (!IncrementalLayoutState.TryGetFullPath(Log, LayoutRoot, nameof(LayoutRoot), out string root))
            {
                return false;
            }

            var destinations = new List<ITaskItem>();
            foreach (ITaskItem source in SourceFiles)
            {
                string name = Path.GetFileName(source.ItemSpec);
                if (name is not ("dnx" or "dnx.cmd"))
                {
                    Log.LogError($"Invalid root shim input '{source.ItemSpec}'.");
                    continue;
                }

                string destination = Path.Combine(root, name);
                destinations.Add(new TaskItem(destination));
            }

            var prepare = new PrepareIncrementalLayout
            {
                BuildEngine = BuildEngine,
                SourceFiles = SourceFiles,
                DestinationFiles = destinations.ToArray(),
                ExistingOutputs = new[] { "dnx", "dnx.cmd" }
                    .Select(name => Path.Combine(root, name))
                    .Where(File.Exists)
                    .Select(path => (ITaskItem)new TaskItem(path)).ToArray()
            };
            foreach (string name in new[] { "dnx", "dnx.cmd" })
            {
                LayoutPathValidation.RejectDirectory(Path.Combine(root, name));
            }

            if (Log.HasLoggedErrors || !prepare.Execute())
            {
                return false;
            }

            ExpectedOutputs = prepare.ExpectedOutputs;
            StaleOutputs = prepare.StaleOutputs;
            for (int index = 0; index < ExpectedOutputs.Length; index++)
            {
                string destination = ExpectedOutputs[index].ItemSpec;
                if (File.Exists(destination))
                {
                    ContentsNeedRepair |= !File.ReadAllBytes(SourceFiles[index].ItemSpec).AsSpan()
                        .SequenceEqual(File.ReadAllBytes(destination));
                }
            }
            if (!OperatingSystem.IsWindows())
            {
                const UnixFileMode ExpectedMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
                foreach (ITaskItem item in ExpectedOutputs)
                {
                    if (Path.GetFileName(item.ItemSpec) == "dnx" && File.Exists(item.ItemSpec))
                    {
                        PermissionsNeedRepair |= File.GetUnixFileMode(item.ItemSpec) != ExpectedMode;
                    }
                }
            }
        }
        catch (Exception exception)
        {
            Log.LogErrorFromException(exception, showStackTrace: true);
        }

        return !Log.HasLoggedErrors;
    }
}
