// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;

namespace Microsoft.NET.Sdk.Razor.Tool.Tests;

[TestClass]
public sealed class ShadowCopyManagerTest : SdkTest
{
    [TestMethod]
    public void GetBaseDirectory_UsesPlatformSpecificUserDirectory()
    {
        var root = OperatingSystem.IsWindows()
            ? Path.GetTempPath()
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.IsFalse(string.IsNullOrEmpty(root));
        Assert.AreEqual(Path.Combine(root, "Razor-Server"), ShadowCopyManager.GetBaseDirectory("Razor-Server"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public void AddAssembly_CreatesPrivateDirectories(bool existingBaseDirectory)
    {
        var directory = TestAssetsManager.CreateTestDirectory();
        var sourceDirectory = Path.Combine(directory.Path, "source");
        Directory.CreateDirectory(Path.Combine(sourceDirectory, "fr", "Extension.resources"));
        var assembly = Path.Combine(sourceDirectory, "Extension.dll");
        var resource = Path.Combine(sourceDirectory, "fr", "Extension.resources.dll");
        var nestedResource = Path.Combine(sourceDirectory, "fr", "Extension.resources", "Extension.resources.dll");
        File.WriteAllText(assembly, "assembly contents");
        File.WriteAllText(resource, "resource contents");
        File.WriteAllText(nestedResource, "nested resource contents");

        var baseDirectory = Path.Combine(directory.Path, "shadow");
        var publicMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
        if (existingBaseDirectory)
        {
            Directory.CreateDirectory(baseDirectory);
            File.SetUnixFileMode(baseDirectory, publicMode);
        }

        using var manager = new ShadowCopyManager(baseDirectory);
        var copy = manager.AddAssembly(assembly);

        Assert.AreEqual(File.ReadAllText(assembly), File.ReadAllText(copy));
        var copyDirectory = Path.GetDirectoryName(copy)!;
        Assert.AreEqual(File.ReadAllText(resource), File.ReadAllText(Path.Combine(copyDirectory, "fr", "Extension.resources.dll")));
        Assert.AreEqual(File.ReadAllText(nestedResource), File.ReadAllText(Path.Combine(copyDirectory, "fr", "Extension.resources", "Extension.resources.dll")));

        var privateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        Assert.AreEqual(existingBaseDirectory ? publicMode : privateMode, File.GetUnixFileMode(baseDirectory));
        Assert.AreEqual(privateMode, File.GetUnixFileMode(manager.UniqueDirectory));
        foreach (var copiedDirectory in Directory.EnumerateDirectories(manager.UniqueDirectory, "*", SearchOption.AllDirectories))
        {
            Assert.AreEqual(privateMode, File.GetUnixFileMode(copiedDirectory), copiedDirectory);
        }
    }

    [TestMethod]
    public void Dispose_ReleasesDirectoryLifetimeMutex()
    {
        var directory = TestAssetsManager.CreateTestDirectory();
        var manager = new ShadowCopyManager(directory.Path);
        var mutexName = Path.GetFileName(manager.UniqueDirectory);

        manager.Dispose();

        Assert.IsFalse(Mutex.TryOpenExisting(mutexName, out var mutex));
        mutex?.Dispose();
    }
}
