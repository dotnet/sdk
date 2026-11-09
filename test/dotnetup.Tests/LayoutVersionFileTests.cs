// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using FluentAssertions;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class LayoutVersionFileTests
{
    [TestMethod]
    [DataRow("0427b9ab\n10.0.114\nwin-x64\n10.0.114-servicing.26522.126\n10.0.100", "10.0.114", "10.0.114-servicing.26522.126")]
    [DataRow("a2f466a2\n12.0.100-alpha.1.26480.102\nwin-x64\n12.0.100-alpha.1.26480.102\n12.0.100", "12.0.100-alpha.1.26480.102", "12.0.100-alpha.1.26480.102")]
    [DataRow("c1d2e3f4\n11.0.100\nosx-arm64\n11.0.100-rtm.26552.115\n11.0.100", "11.0.100", "11.0.100-rtm.26552.115")]
    [DataRow("95017c71\n10.0.12", "10.0.12", null)]
    [DataRow("commit\r\n\r\n  10.0.12  \r\n", "10.0.12", null)]
    [DataRow("commit\nnot-a-version\nrid\nalso-not-a-version", null, null)]
    public void Read_ParsesProductAndBuildVersions(string content, string? productVersion, string? buildVersion)
    {
        var file = LayoutVersionFile.Read(new StringReader(content))!;

        (file.ProductVersion?.ToString()).Should().Be(productVersion);
        (file.BuildVersion?.ToString()).Should().Be(buildVersion);
    }

    [TestMethod]
    [DataRow("10.0.403", "commit\n10.0.403\nrid\n10.0.403-servicing.26523.112\n10.0.400", "10.0.403-servicing.26523.112")]
    [DataRow("12.0.100-alpha.1", "commit\n12.0.100-alpha.1\nrid\n12.0.100-alpha.1\n12.0.100", "12.0.100-alpha.1")]
    [DataRow("10.0.403", "commit\n10.0.402\nrid\n10.0.402-servicing.26423.113\n10.0.400", null)]
    [DataRow("10.0.403", "commit\n10.0.403\nrid\n10.0.404-servicing.26423.113\n10.0.400", null)]
    [DataRow("10.0.12", "commit\n10.0.12", null)]
    public void GetBuildVersionFor_RequiresTheFileToDescribeTheFolder(string folder, string content, string? expected)
    {
        var file = LayoutVersionFile.Read(new StringReader(content))!;

        (file.GetBuildVersionFor(new Microsoft.Deployment.DotNet.Releases.ReleaseVersion(folder))?.ToString()).Should().Be(expected);
    }

    [TestMethod]
    public void Read_NormalizesContent()
    {
        var file = LayoutVersionFile.Read(new StringReader("  commit \r\n\r\n10.0.12\r\n"))!;

        file.Content.Should().Be("commit\n10.0.12");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" \n\r\n ")]
    public void Read_ReturnsNullForBlankContent(string content)
    {
        LayoutVersionFile.Read(new StringReader(content)).Should().BeNull();
    }

    [TestMethod]
    public void IsSameBuild_ComparesNormalizedContentCaseInsensitively()
    {
        var file = LayoutVersionFile.Read(new StringReader("ABCDEF\n10.0.12"))!;

        file.IsSameBuild(LayoutVersionFile.Read(new StringReader("abcdef\r\n10.0.12\r\n"))).Should().BeTrue();
        file.IsSameBuild(LayoutVersionFile.Read(new StringReader("123456\n10.0.12"))).Should().BeFalse();
        file.IsSameBuild(null).Should().BeFalse();
    }

    [TestMethod]
    public void ReadFromDirectory_ReturnsNullWhenTheFileIsMissing()
    {
        using var testEnv = new TestEnvironment();

        LayoutVersionFile.ReadFromDirectory(testEnv.InstallPath).Should().BeNull();
    }

    [TestMethod]
    public void ReadFromDirectory_ReadsTheFolderVersionFile()
    {
        using var testEnv = new TestEnvironment();
        File.WriteAllText(Path.Combine(testEnv.InstallPath, ".version"), "commit\n10.0.403\nrid\n10.0.403-servicing.26523.112\n10.0.400\n");

        var file = LayoutVersionFile.ReadFromDirectory(testEnv.InstallPath)!;

        file.ProductVersion!.ToString().Should().Be("10.0.403");
        file.BuildVersion!.ToString().Should().Be("10.0.403-servicing.26523.112");
    }

    [TestMethod]
    public void ReadFromArchive_KeysVersionFilesBySubcomponent()
    {
        using var testEnv = new TestEnvironment();
        string archivePath = Path.Combine(testEnv.TempRoot, "payload.zip");
        using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            AddEntry(zip, "sdk/11.0.100/.version", "sdk-commit\n11.0.100\nrid\n11.0.100-rtm.26552.115\n11.0.100");
            AddEntry(zip, "shared/Microsoft.NETCore.App/11.0.0/.version", "runtime-commit\n11.0.0");
            AddEntry(zip, "sdk/11.0.100/dotnet.dll", "not a version file");
        }

        var files = LayoutVersionFile.ReadFromArchive(archivePath);

        files.Keys.Should().BeEquivalentTo("sdk/11.0.100", "shared/Microsoft.NETCore.App/11.0.0");
        files["sdk/11.0.100"].BuildVersion!.ToString().Should().Be("11.0.100-rtm.26552.115");
        files["shared/Microsoft.NETCore.App/11.0.0"].BuildVersion.Should().BeNull();
    }

    private static void AddEntry(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(content);
    }
}
