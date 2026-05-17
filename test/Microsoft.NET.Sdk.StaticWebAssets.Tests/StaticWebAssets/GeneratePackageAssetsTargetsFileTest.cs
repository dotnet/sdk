// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Xml.Linq;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class GeneratePackageAssetsTargetsFileTest : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IBuildEngine> _buildEngine;
    private readonly List<string> _errorMessages;

    public GeneratePackageAssetsTargetsFileTest()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GenPkgTargets_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _errorMessages = new List<string>();
        _buildEngine = new Mock<IBuildEngine>();
        _buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => _errorMessages.Add(args.Message));
        _buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void GeneratesValidXml_WithStaticWebAssetPackageManifestItem()
    {
        var task = CreateTask();

        task.Execute().Should().BeTrue();
        File.Exists(task.TargetFilePath).Should().BeTrue();

        var doc = XDocument.Load(task.TargetFilePath);
        var root = doc.Root;
        root.Should().NotBeNull();
        root.Name.LocalName.Should().Be("Project");

        var itemGroup = root.Element("ItemGroup");
        itemGroup.Should().NotBeNull();

        var manifestItem = itemGroup.Element("StaticWebAssetPackageManifest");
        manifestItem.Should().NotBeNull();

        var includeAttr = manifestItem.Attribute("Include");
        includeAttr.Should().NotBeNull();
        includeAttr.Value.Should().Contain("MyLib.PackageAssets.json");

        var sourceIdElement = manifestItem.Element("SourceId");
        sourceIdElement.Should().NotBeNull();
        sourceIdElement.Value.Should().Be("MyLib");

        var contentRootElement = manifestItem.Element("ContentRoot");
        contentRootElement.Should().NotBeNull();
        contentRootElement.Value.Should().Contain("staticwebassets");

        var packageRootElement = manifestItem.Element("PackageRoot");
        packageRootElement.Should().NotBeNull();
    }

    [Fact]
    public void Incremental_FileNotRewritten_WhenContentUnchanged()
    {
        var task = CreateTask();

        // First write
        task.Execute().Should().BeTrue();
        var firstWriteTime = File.GetLastWriteTimeUtc(task.TargetFilePath);
        var firstContent = File.ReadAllText(task.TargetFilePath);

        // Advance the file timestamp so we can detect if it gets rewritten
        File.SetLastWriteTimeUtc(task.TargetFilePath, firstWriteTime.AddSeconds(10));
        firstWriteTime = File.GetLastWriteTimeUtc(task.TargetFilePath);

        // Second write with same inputs
        var task2 = CreateTask();
        task2.Execute().Should().BeTrue();
        var secondWriteTime = File.GetLastWriteTimeUtc(task2.TargetFilePath);
        var secondContent = File.ReadAllText(task2.TargetFilePath);

        // Content should be identical
        secondContent.Should().Be(firstContent);
        // File should not have been rewritten (timestamp preserved)
        secondWriteTime.Should().Be(firstWriteTime);
    }

    [Fact]
    public void CustomPackagePathPrefix_ReflectedInContentRoot()
    {
        var task = CreateTask(packagePathPrefix: "custom/assets");

        task.Execute().Should().BeTrue();
        var manifestItem = LoadManifestItem(task.TargetFilePath);

        manifestItem.Element("ContentRoot").Value.Should().Contain("custom");
    }

    private GeneratePackageAssetsTargetsFile CreateTask(string packagePathPrefix = null)
    {
        var task = new GeneratePackageAssetsTargetsFile
        {
            BuildEngine = _buildEngine.Object,
            PackageId = "MyLib",
            TargetFilePath = Path.Combine(_tempDir, "MyLib.targets"),
            ManifestFileName = "MyLib.PackageAssets.json",
        };
        if (packagePathPrefix != null)
        {
            task.PackagePathPrefix = packagePathPrefix;
        }
        return task;
    }

    private static XElement LoadManifestItem(string targetFilePath)
    {
        var doc = XDocument.Load(targetFilePath);
        return doc.Root.Element("ItemGroup").Element("StaticWebAssetPackageManifest");
    }
}
