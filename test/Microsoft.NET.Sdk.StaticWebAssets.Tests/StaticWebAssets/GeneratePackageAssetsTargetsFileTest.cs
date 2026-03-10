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
        var targetPath = Path.Combine(_tempDir, "MyLib.targets");

        var task = new GeneratePackageAssetsTargetsFile
        {
            BuildEngine = _buildEngine.Object,
            PackageId = "MyLib",
            TargetFilePath = targetPath,
            ManifestFileName = "MyLib.PackageAssets.json",
        };

        var result = task.Execute();

        result.Should().BeTrue();
        File.Exists(targetPath).Should().BeTrue();

        var doc = XDocument.Load(targetPath);
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
        var targetPath = Path.Combine(_tempDir, "MyLib.targets");

        var task = new GeneratePackageAssetsTargetsFile
        {
            BuildEngine = _buildEngine.Object,
            PackageId = "MyLib",
            TargetFilePath = targetPath,
            ManifestFileName = "MyLib.PackageAssets.json",
        };

        // First write
        task.Execute().Should().BeTrue();
        var firstWriteTime = File.GetLastWriteTimeUtc(targetPath);
        var firstContent = File.ReadAllText(targetPath);

        // Wait a moment so the clock advances
        System.Threading.Thread.Sleep(50);

        // Second write with same inputs
        var task2 = new GeneratePackageAssetsTargetsFile
        {
            BuildEngine = _buildEngine.Object,
            PackageId = "MyLib",
            TargetFilePath = targetPath,
            ManifestFileName = "MyLib.PackageAssets.json",
        };

        task2.Execute().Should().BeTrue();
        var secondWriteTime = File.GetLastWriteTimeUtc(targetPath);
        var secondContent = File.ReadAllText(targetPath);

        // Content should be identical
        secondContent.Should().Be(firstContent);
        // File should not have been rewritten (timestamp preserved)
        secondWriteTime.Should().Be(firstWriteTime);
    }

    [Fact]
    public void CustomPackagePathPrefix_ReflectedInContentRoot()
    {
        var targetPath = Path.Combine(_tempDir, "MyLib.targets");

        var task = new GeneratePackageAssetsTargetsFile
        {
            BuildEngine = _buildEngine.Object,
            PackageId = "MyLib",
            TargetFilePath = targetPath,
            ManifestFileName = "MyLib.PackageAssets.json",
            PackagePathPrefix = "custom/assets",
        };

        var result = task.Execute();

        result.Should().BeTrue();

        var doc = XDocument.Load(targetPath);
        var contentRoot = doc.Root
            .Element("ItemGroup")
            .Element("StaticWebAssetPackageManifest")
            .Element("ContentRoot");

        contentRoot.Value.Should().Contain("custom");
    }
}
