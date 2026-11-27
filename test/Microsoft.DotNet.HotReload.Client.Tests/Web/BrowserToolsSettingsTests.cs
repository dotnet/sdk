// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.HotReload.UnitTests;

[TestClass]
public sealed class BrowserToolsSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "browser-tools-settings", Guid.NewGuid().ToString("N"));

    [TestMethod]
    public void WriteSettings_ChangesOnlyDifferentBytes()
    {
        var path = Path.Combine(_directory, SharedSecretBuildOutputs.DirectoryName, BrowserToolsSettings.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var settings = BrowserToolsSettings.FromProjectSettings(_directory);
        File.WriteAllText(path, "{ \"hotReload\": false }" + Environment.NewLine);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-2));
        var disabled = File.GetLastWriteTimeUtc(path);

        settings.Enable();
        Assert.AreEqual("{ \"hotReload\": true }" + Environment.NewLine, File.ReadAllText(path));
        Assert.IsGreaterThan(disabled, File.GetLastWriteTimeUtc(path));
        var enabled = File.GetLastWriteTimeUtc(path);

        settings.Enable();
        Assert.AreEqual(enabled, File.GetLastWriteTimeUtc(path));

        settings.Disable();
        Assert.AreEqual("{ \"hotReload\": false }" + Environment.NewLine, File.ReadAllText(path));
        var restored = File.GetLastWriteTimeUtc(path);

        settings.Disable();
        Assert.AreEqual(restored, File.GetLastWriteTimeUtc(path));
    }

    [TestMethod]
    public void Enable_RequiresSettingsProducedByBuild()
    {
        var settings = BrowserToolsSettings.FromProjectSettings(_directory);
        Assert.ThrowsExactly<DirectoryNotFoundException>(settings.Enable);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
