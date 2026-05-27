// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Tasks.ConflictResolution;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests;

[Collection(nameof(CwdSensitiveCollection))]
public class GivenAResolvePackageFileConflictsMultiThreading : IDisposable
{
    private readonly string _originalCwd = Directory.GetCurrentDirectory();
    private readonly string _testRoot = Path.Combine(AppContext.BaseDirectory, "rpfc_test_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalCwd);
        try { Directory.Delete(_testRoot, true); } catch { }
    }

    /// <summary>
    /// PlatformManifests with a relative ItemSpec must be resolved against the task's
    /// ProjectDirectory (via TaskEnvironment), not the process CWD. We verify this by
    /// pointing at a manifest that exists relative to the project directory but not the
    /// CWD: if path resolution were CWD-based, the task would log CouldNotLoadPlatformManifest.
    /// </summary>
    [Fact]
    public void PlatformManifest_ResolvesRelativePathAgainstProjectDir()
    {
        var projectDir = CreateTempDir();
        var decoyDir = CreateTempDir();

        var manifestRelPath = Path.Combine("manifests", "PlatformManifest.txt");
        var manifestPath = Path.Combine(projectDir, manifestRelPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        File.WriteAllText(manifestPath, "System.Runtime.dll|Platform.Package|9.0.0.0|9.0.0.0");

        var copyLocalRelPath = Path.Combine("packages", "System.Runtime.dll");
        CreateFile(Path.Combine(projectDir, copyLocalRelPath));
        var copyLocalItem = new MockTaskItem(copyLocalRelPath, new Dictionary<string, string>
        {
            { "NuGetPackageId", "Package.Low" },
            { "AssemblyVersion", "1.0.0.0" },
            { "FileVersion", "1.0.0.0" }
        });

        Directory.SetCurrentDirectory(decoyDir);

        var engine = new MockBuildEngine();
        var task = CreateTask(engine);
        task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
        task.PlatformManifests = new ITaskItem[] { new MockTaskItem(manifestRelPath, new Dictionary<string, string>()) };
        task.ReferenceCopyLocalPaths = new ITaskItem[] { copyLocalItem };

        task.Execute().Should().BeTrue("relative PlatformManifests should resolve against ProjectDirectory");
        engine.Errors.Should().BeEmpty("manifest was found via ProjectDirectory-relative resolution");
        task.ReferenceCopyLocalPathsWithoutConflicts.Should().BeEmpty(
            "the platform manifest item from ProjectDirectory should win the runtime conflict");

        var conflict = task.Conflicts.Should().ContainSingle().Which;
        conflict.ItemSpec.Should().Be(copyLocalRelPath);
        conflict.GetMetadata("NuGetPackageId").Should().Be("Package.Low");
        conflict.GetMetadata(nameof(ConflictItemType)).Should().Be(ConflictItemType.CopyLocal.ToString());
    }

    /// <summary>
    /// When a relative PlatformManifest cannot be found, the error message must show the
    /// original (relative) path the user supplied, not the absolutized path used for I/O.
    /// This covers Sin 2 (user-facing path relativity) for the AbsolutePath rewrite.
    /// </summary>
    [Fact]
    public void PlatformManifest_MissingFile_LogsOriginalRelativePath()
    {
        var projectDir = CreateTempDir();
        var decoyDir = CreateTempDir();

        var manifestRelPath = Path.Combine("manifests", "DoesNotExist.txt");
        // Intentionally do not create the file.

        Directory.SetCurrentDirectory(decoyDir);

        var engine = new MockBuildEngine();
        var task = CreateTask(engine);
        task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
        task.PlatformManifests = new ITaskItem[] { new MockTaskItem(manifestRelPath, new Dictionary<string, string>()) };

        task.Execute().Should().BeFalse("a missing PlatformManifest should fail the task");
        var error = engine.Errors.Should().ContainSingle().Which;
        error.Message.Should().Contain(manifestRelPath, "the original relative path must be preserved in the error");
        error.Message.Should().NotContain(projectDir, "the absolutized path must not leak into user-facing errors");
    }

    /// <summary>
    /// TargetFrameworkDirectories with a relative ItemSpec must be resolved against the task's
    /// ProjectDirectory (via TaskEnvironment), not the process CWD. We verify this by placing
    /// a FrameworkList.xml that exists only relative to the project directory and asserting the
    /// framework list cache key uses the project-directory-relative absolute path.
    /// </summary>
    [Fact]
    public void TargetFrameworkDirectories_ResolveRelativePathAgainstProjectDir()
    {
        var projectDir = CreateTempDir();
        var decoyDir = CreateTempDir();

        var targetFrameworkRelPath = "refpack";
        var expectedFrameworkListPath = Path.Combine(projectDir, targetFrameworkRelPath, "RedistList", "FrameworkList.xml");
        CreateFrameworkList(Path.Combine(projectDir, targetFrameworkRelPath), "System.Runtime", "9.0.0.0");

        var referenceRelPath = Path.Combine("packages", "System.Runtime.dll");
        CreateFile(Path.Combine(projectDir, referenceRelPath));

        Directory.SetCurrentDirectory(decoyDir);

        var engine = new MockBuildEngine();
        var task = CreateTask(engine);
        task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
        task.TargetFrameworkDirectories = new ITaskItem[] { new MockTaskItem(targetFrameworkRelPath, new Dictionary<string, string>()) };
        task.References = new ITaskItem[]
        {
            new MockTaskItem(referenceRelPath, new Dictionary<string, string>
            {
                { "NuGetPackageId", "Package.Low" },
                { "AssemblyVersion", "1.0.0.0" },
                { "FileVersion", "1.0.0.0" }
            })
        };

        task.Execute().Should().BeTrue("relative TargetFrameworkDirectories should resolve against ProjectDirectory");
        engine.Errors.Should().BeEmpty();
        engine.RegisteredTaskObjects.Keys.Should().Contain(key =>
            key.ToString()!.EndsWith(expectedFrameworkListPath, StringComparison.OrdinalIgnoreCase));
        task.ReferencesWithoutConflicts.Should().ContainSingle();
        task.ReferencesWithoutConflicts![0].ItemSpec.Should().Be("System.Runtime");
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(_testRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CreateFrameworkList(string targetFrameworkDirectory, string assemblyName, string version)
    {
        var redistListDir = Path.Combine(targetFrameworkDirectory, "RedistList");
        Directory.CreateDirectory(redistListDir);
        File.WriteAllText(Path.Combine(redistListDir, "FrameworkList.xml"),
            $"<FileList><File AssemblyName=\"{assemblyName}\" Version=\"{version}\" /></FileList>");
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
}
