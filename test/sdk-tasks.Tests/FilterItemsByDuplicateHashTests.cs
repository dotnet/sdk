// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests;

public class FilterItemsByDuplicateHashTests(ITestOutputHelper log) : SdkTest(log)
{
#if !NETFRAMEWORK
    [Fact]
    public void UnmatchedFilesContainsOnlyCandidatesNotInReference()
    {
        var testDir = TestAssetsManager.CreateTestDirectory().Path;

        // Create reference files
        var refFile1 = CreateFile(testDir, "ref", "shared.dll", "shared content");
        var refFile2 = CreateFile(testDir, "ref", "other.dll", "other content");

        // Create candidate files: one matching, one unique
        var candShared = CreateFile(testDir, "candidates", "shared.dll", "shared content");
        var candUnique = CreateFile(testDir, "candidates", "unique.dll", "unique content");

        var task = CreateTask(
            candidates: [ToTaskItem(candShared), ToTaskItem(candUnique)],
            references: [ToTaskItem(refFile1), ToTaskItem(refFile2)]);

        var result = task.Execute();

        result.Should().BeTrue();
        task.UnmatchedFiles.Should().HaveCount(1);
        task.UnmatchedFiles[0].ItemSpec.Should().Be(candUnique);
    }

    [Fact]
    public void MatchingIsByContentNotByFileName()
    {
        var testDir = TestAssetsManager.CreateTestDirectory().Path;

        // Same filename but different content should be unmatched
        var refFile = CreateFile(testDir, "ref", "lib.dll", "version 1");
        var candFile = CreateFile(testDir, "candidates", "lib.dll", "version 2");

        var task = CreateTask(
            candidates: [ToTaskItem(candFile)],
            references: [ToTaskItem(refFile)]);

        var result = task.Execute();

        result.Should().BeTrue();
        task.UnmatchedFiles.Should().HaveCount(1);
    }

    private static string CreateFile(string root, string subDir, string fileName, string content)
    {
        var dir = Path.Combine(root, subDir);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static ITaskItem ToTaskItem(string path) => new TaskItem(path);

    private static FilterItemsByDuplicateHash CreateTask(ITaskItem[] candidates, ITaskItem[] references)
    {
        return new FilterItemsByDuplicateHash
        {
            CandidateFiles = candidates,
            ReferenceFiles = references,
            BuildEngine = new MockBuildEngine()
        };
    }
#endif
}
