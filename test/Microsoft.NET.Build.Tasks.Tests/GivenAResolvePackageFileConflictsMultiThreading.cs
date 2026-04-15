// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Tasks.ConflictResolution;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests;

[Collection("CwdSensitive")]
public class GivenAResolvePackageFileConflictsMultiThreading : IDisposable
{
    private readonly List<string> _tempDirs = new();
    private readonly string _originalCwd = Directory.GetCurrentDirectory();

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalCwd);
        foreach (var dir in _tempDirs)
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rpfc_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    private static void CreateFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Array.Empty<byte>());
    }

    private static ResolvePackageFileConflicts CreateTask(MockBuildEngine engine)
    {
        var task = new ResolvePackageFileConflicts();
        task.BuildEngine = engine;
        return task;
    }

    /// <summary>
    /// When TaskEnvironment points to projectDir (different from CWD), the task should
    /// resolve relative paths against projectDir and find files there, not under CWD.
    /// </summary>
    [Fact]
    public void PathResolution_ResolvesRelativePathsAgainstProjectDir()
    {
        var projectDir = CreateTempDir();
        var decoyDir = CreateTempDir();

        // Create files under projectDir at a relative sub-path
        var relPath1 = Path.Combine("lib", "System.Runtime.dll");
        var relPath2 = Path.Combine("lib2", "System.Runtime.dll");
        CreateFile(Path.Combine(projectDir, relPath1));
        CreateFile(Path.Combine(projectDir, relPath2));

        // Items with relative paths and version metadata so ConflictItem doesn't need real DLLs
        var ref1 = new MockTaskItem(relPath1, new Dictionary<string, string>
        {
            { "NuGetPackageId", "Package.A" },
            { "AssemblyVersion", "1.0.0.0" },
            { "FileVersion", "1.0.0.0" }
        });
        var ref2 = new MockTaskItem(relPath2, new Dictionary<string, string>
        {
            { "NuGetPackageId", "Package.B" },
            { "AssemblyVersion", "2.0.0.0" },
            { "FileVersion", "2.0.0.0" }
        });

        // Set CWD to decoy - files do NOT exist relative to decoy
        Directory.SetCurrentDirectory(decoyDir);

        var engine = new MockBuildEngine();
        var task = CreateTask(engine);
        task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
        task.References = new ITaskItem[] { ref1, ref2 };

        task.Execute().Should().BeTrue("task should succeed");

        // Both files exist under projectDir, so the conflict resolver should detect the conflict
        // and pick Package.B (higher AssemblyVersion) as the winner.
        // If path resolution is broken, File.Exists returns false for both → no conflict detected.
        task.ReferencesWithoutConflicts.Should().NotBeNull();
        task.ReferencesWithoutConflicts!.Length.Should().Be(1, "one reference should be removed as a conflict loser");
        task.ReferencesWithoutConflicts[0].Should().BeSameAs(ref2, "higher version should win");
    }

    /// <summary>
    /// Running the task with CWD==projectDir should produce the same results as running with
    /// CWD==otherDir when TaskEnvironment points to projectDir.
    /// </summary>
    [Fact]
    public void Parity_CwdEqualsProjectDir_MatchesCwdDifferentFromProjectDir()
    {
        var projectDir = CreateTempDir();
        var decoyDir = CreateTempDir();

        var relPath1 = Path.Combine("pkg", "MyLib.dll");
        var relPath2 = Path.Combine("pkg2", "MyLib.dll");
        CreateFile(Path.Combine(projectDir, relPath1));
        CreateFile(Path.Combine(projectDir, relPath2));

        var makeItems = () => new ITaskItem[]
        {
            new MockTaskItem(relPath1, new Dictionary<string, string>
            {
                { "NuGetPackageId", "Lib.One" },
                { "AssemblyVersion", "1.0.0.0" },
                { "FileVersion", "1.0.0.0" }
            }),
            new MockTaskItem(relPath2, new Dictionary<string, string>
            {
                { "NuGetPackageId", "Lib.Two" },
                { "AssemblyVersion", "3.0.0.0" },
                { "FileVersion", "3.0.0.0" }
            }),
        };

        // Run 1: CWD == projectDir
        Directory.SetCurrentDirectory(projectDir);
        var engine1 = new MockBuildEngine();
        var task1 = CreateTask(engine1);
        task1.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
        task1.References = makeItems();
        task1.Execute().Should().BeTrue();

        // Run 2: CWD == decoyDir
        Directory.SetCurrentDirectory(decoyDir);
        var engine2 = new MockBuildEngine();
        var task2 = CreateTask(engine2);
        task2.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
        task2.References = makeItems();
        task2.Execute().Should().BeTrue();

        task1.ReferencesWithoutConflicts!.Length.Should().Be(task2.ReferencesWithoutConflicts!.Length,
            "same conflict resolution results regardless of CWD");

        var conflicts1 = task1.Conflicts?.Select(c => c.ItemSpec).OrderBy(s => s).ToArray() ?? Array.Empty<string>();
        var conflicts2 = task2.Conflicts?.Select(c => c.ItemSpec).OrderBy(s => s).ToArray() ?? Array.Empty<string>();
        conflicts1.Should().BeEquivalentTo(conflicts2, "conflicts should be identical");
    }

    /// <summary>
    /// Output items should preserve the original relative path form from the input items.
    /// </summary>
    [Fact]
    public void OutputRelativity_PreservesRelativePathsInOutput()
    {
        var projectDir = CreateTempDir();
        var decoyDir = CreateTempDir();

        var relPath = Path.Combine("packages", "Unique.dll");
        CreateFile(Path.Combine(projectDir, relPath));

        var item = new MockTaskItem(relPath, new Dictionary<string, string>
        {
            { "NuGetPackageId", "Unique.Package" },
            { "AssemblyVersion", "1.0.0.0" },
            { "FileVersion", "1.0.0.0" }
        });

        Directory.SetCurrentDirectory(decoyDir);

        var engine = new MockBuildEngine();
        var task = CreateTask(engine);
        task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
        task.References = new ITaskItem[] { item };

        task.Execute().Should().BeTrue();

        // With a single item there's no conflict, so it should pass through unchanged
        task.ReferencesWithoutConflicts.Should().NotBeNull();
        task.ReferencesWithoutConflicts!.Length.Should().Be(1);
        task.ReferencesWithoutConflicts[0].ItemSpec.Should().Be(relPath,
            "output should preserve the original relative path");
    }

    /// <summary>
    /// Multiple task instances running concurrently with isolated project directories
    /// should not interfere with each other.
    /// </summary>
    [Fact]
    public void ConcurrentExecution_IsolatedProjectDirsProduceCorrectResults()
    {
        const int threadCount = 4;
        var decoyDir = CreateTempDir();
        Directory.SetCurrentDirectory(decoyDir);

        var results = new (bool success, int refsWithoutConflicts, int conflicts)[threadCount];
        var exceptions = new Exception?[threadCount];

        Parallel.For(0, threadCount, i =>
        {
            try
            {
                var projectDir = Path.Combine(Path.GetTempPath(), "rpfc_concurrent_" + Guid.NewGuid().ToString("N")[..8]);
                Directory.CreateDirectory(projectDir);

                try
                {
                    var relPath1 = Path.Combine("lib", $"Conflict{i}.dll");
                    var relPath2 = Path.Combine("lib2", $"Conflict{i}.dll");
                    CreateFile(Path.Combine(projectDir, relPath1));
                    CreateFile(Path.Combine(projectDir, relPath2));

                    var refs = new ITaskItem[]
                    {
                        new MockTaskItem(relPath1, new Dictionary<string, string>
                        {
                            { "NuGetPackageId", "Pkg.Low" },
                            { "AssemblyVersion", "1.0.0.0" },
                            { "FileVersion", "1.0.0.0" }
                        }),
                        new MockTaskItem(relPath2, new Dictionary<string, string>
                        {
                            { "NuGetPackageId", "Pkg.High" },
                            { "AssemblyVersion", "5.0.0.0" },
                            { "FileVersion", "5.0.0.0" }
                        }),
                    };

                    var engine = new MockBuildEngine();
                    var task = CreateTask(engine);
                    task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
                    task.References = refs;

                    var success = task.Execute();
                    results[i] = (success,
                        task.ReferencesWithoutConflicts?.Length ?? 0,
                        task.Conflicts?.Length ?? 0);
                }
                finally
                {
                    try { Directory.Delete(projectDir, true); } catch { }
                }
            }
            catch (Exception ex)
            {
                exceptions[i] = ex;
            }
        });

        for (int i = 0; i < threadCount; i++)
        {
            exceptions[i].Should().BeNull($"thread {i} should not throw");
            results[i].success.Should().BeTrue($"thread {i} task should succeed");
            results[i].refsWithoutConflicts.Should().Be(1, $"thread {i} should have 1 winner");
            results[i].conflicts.Should().Be(1, $"thread {i} should have 1 conflict");
        }

        // CWD restoration is handled in Dispose; avoid asserting on CWD here because
        // parallel test classes in the same collection could interleave before this check.
    }
}
