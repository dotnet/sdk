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

    [Fact]
    public void PlatformManifest_EmptyPathLogsAndSkips()
    {
        var projectDir = CreateTempDir();

        var engine = new MockBuildEngine();
        var task = CreateTask(engine);
        task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
        task.PlatformManifests = new ITaskItem[] { new MockTaskItem(string.Empty, new Dictionary<string, string>()) };

        task.Execute().Should().BeFalse("empty PlatformManifests should log and skip without throwing");
        var error = engine.Errors.Should().ContainSingle().Which;
        error.Message.Should().NotContain(projectDir, "the original empty path must not be absolutized");
    }

    /// <summary>
    /// TargetFrameworkDirectories with a relative ItemSpec must preserve the pre-migration
    /// behavior: the derived FrameworkList.xml path is invalid because it is not rooted.
    /// </summary>
    [Fact]
    public void TargetFrameworkDirectories_RelativePathLogsNotRootedOriginalPath()
    {
        var projectDir = CreateTempDir();
        var decoyDir = CreateTempDir();

        var targetFrameworkRelPath = "refpack";
        var expectedFrameworkListPath = Path.Combine(targetFrameworkRelPath, "RedistList", "FrameworkList.xml");
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

        task.Execute().Should().BeFalse("relative TargetFrameworkDirectories should remain invalid");
        var error = engine.Errors.Should().ContainSingle().Which;
        error.Message.Should().Contain(expectedFrameworkListPath, "the original relative path must be preserved in the error");
        error.Message.Should().NotContain(projectDir, "the relative path must not be silently absolutized against ProjectDirectory");
        engine.RegisteredTaskObjects.Should().BeEmpty();
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
