// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.UnitTests;

/// <summary>
/// Test infrastructure that simulates how MSBuild multi-node execution
/// can cause tasks to run with a CWD different from the project directory.
///
/// Creates two temp directories:
///   - ProjectDirectory: where project files live (the "correct" base for relative paths)
///   - SpawnDirectory: a different CWD simulating a remote MSBuild node
///
/// On construction the process CWD is changed to SpawnDirectory so that any
/// task using <c>Environment.CurrentDirectory</c> to resolve relative paths will
/// resolve against the wrong directory, exposing the bug.
/// </summary>
internal sealed class TaskTestEnvironment : IDisposable
{
    private readonly string _root;
    private readonly string _savedCwd;

    /// <summary>Where project files are created.</summary>
    public string ProjectDirectory { get; }

    /// <summary>A separate directory used as the process CWD during the test.</summary>
    public string SpawnDirectory { get; }

    /// <summary>
    /// A <see cref="TaskEnvironment"/> whose <c>ProjectDirectory</c> points to
    /// <see cref="ProjectDirectory"/>, enabling correct relative-path resolution.
    /// </summary>
    public TaskEnvironment TaskEnvironment { get; }

    public TaskTestEnvironment()
    {
        _root = Path.Combine(Path.GetTempPath(), "TaskTests-" + Guid.NewGuid().ToString("N"));
        ProjectDirectory = Path.Combine(_root, "project");
        SpawnDirectory = Path.Combine(_root, "spawn");

        Directory.CreateDirectory(ProjectDirectory);
        Directory.CreateDirectory(SpawnDirectory);

        TaskEnvironment = TaskEnvironmentHelper.CreateForTest(ProjectDirectory);

        // Switch CWD to the spawn directory so relative paths don't accidentally work.
        _savedCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(SpawnDirectory);
    }

    /// <summary>
    /// Creates a file under <see cref="ProjectDirectory"/> at the given relative path.
    /// Parent directories are created automatically.
    /// </summary>
    /// <returns>The absolute path of the created file.</returns>
    public string CreateProjectFile(string relativePath, string content)
    {
        var absolutePath = GetProjectPath(relativePath);
        var dir = Path.GetDirectoryName(absolutePath)!;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(absolutePath, content);
        return absolutePath;
    }

    /// <summary>
    /// Creates a directory under <see cref="ProjectDirectory"/> at the given relative path.
    /// </summary>
    public void CreateProjectDirectory(string relativePath)
    {
        Directory.CreateDirectory(GetProjectPath(relativePath));
    }

    /// <summary>
    /// Returns the absolute path that a relative path would resolve to under the project directory.
    /// This is the "correct" path.
    /// </summary>
    public string GetProjectPath(string relativePath)
    {
        return Path.Combine(ProjectDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// Returns the absolute path that a relative path would resolve to under the spawn directory.
    /// This is the "incorrect" path that a buggy task would use.
    /// </summary>
    public string GetIncorrectPath(string relativePath)
    {
        return Path.Combine(SpawnDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_savedCwd);

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; temp files will be cleaned up by the OS.
        }
    }
}
