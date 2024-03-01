// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Task = System.Threading.Tasks.Task;

public class FindArchiveDiffs : Microsoft.Build.Utilities.Task, ICancelableTask
{
    [Required]
    public required ITaskItem BaselineArchive { get; init; }

    [Required]
    public required ITaskItem TestArchive { get; init; }

    [Output]
    public ITaskItem[] ContentDifferences { get; set; } = [];

    private CancellationTokenSource _cancellationTokenSource = new();
    private CancellationToken cancellationToken => _cancellationTokenSource.Token;

    public override bool Execute()
    {
        return Task.Run(ExecuteAsync).Result;
    }

    public async Task<bool> ExecuteAsync()
    {
        var baselineTask = Archive.Create(BaselineArchive.ItemSpec);
        var testTask = Archive.Create(TestArchive.ItemSpec);
        Task.WaitAll([baselineTask, testTask], cancellationToken);
        using var baseline = await baselineTask;
        using var test = await testTask;
        var baselineFiles = baseline.GetFileNames();
        var testFiles = test.GetFileNames();
        ContentDifferences =
            Diff.GetDiffs(baselineFiles, testFiles, VersionIdentifier.AreVersionlessEqual, static p => VersionIdentifier.RemoveVersions(p, "{VERSION}"), cancellationToken)
            .Select(Diff.TaskItemFromDiff)
            .ToArray();
        return true;
    }

    public void Cancel()
    {
        _cancellationTokenSource.Cancel();
    }
}
