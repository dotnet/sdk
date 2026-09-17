// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using WixToolset.Dtf.WindowsInstaller;

namespace Microsoft.CoreSdkTasks.Tests;

[TestClass]
public class TemplateMsiIncrementalityTests : SdkTest
{
    /// <summary>
    /// Verifies the pinned WiX targets skip unchanged template MSI builds and rebuild the
    /// appropriate stages after installer-property changes, payload edits, and removals.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void PinnedWixTargetsHonorTemplateMsiIncrementalInputs()
    {
        string root = TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString("N")).Path;
        string packageFeed = Path.Combine(AppContext.BaseDirectory, "WixPackages");
        string authoringSource = Path.Combine(AppContext.BaseDirectory, "TemplateMsiAuthoring");
        Directory.Exists(packageFeed).Should().BeTrue($"the WiX package payload should exist at {packageFeed}");
        Directory.Exists(authoringSource).Should().BeTrue($"the installer authoring payload should exist at {authoringSource}");

        CopyDirectory(authoringSource, root);

        string wixVersion = GetPackageVersion(packageFeed, "Microsoft.WixToolset.Sdk");
        string installersVersion = GetPackageVersion(packageFeed, "Microsoft.DotNet.Build.Tasks.Installers");
        WriteBuildEnvironment(root, packageFeed, wixVersion, installersVersion);

        string projectPath = Path.Combine(root, "pkg", "windows", "msis", "templates", "templates.wixproj");
        string packageCache = Path.Combine(root, "packages");
        string intermediatePath = Path.Combine(root, "obj");
        string outputPath = Path.Combine(root, "output");
        string installerPath = Path.Combine(outputPath, "templates.msi");
        string layoutPath = Path.Combine(root, "layout");
        string retainedPackage = CreateFile(layoutPath, "retained.nupkg", "retained");
        string removedPackage = CreateFile(layoutPath, "removed.nupkg", "remove");
        string completionStamp = CreateFile(root, "layout.complete", "complete");
        string buildPropertiesFile = Path.Combine(intermediatePath, "template-msi-build.properties");

        var properties = new Dictionary<string, string>
        {
            ["InstallerPath"] = installerPath,
            ["OutputPath"] = EnsureTrailingSeparator(outputPath),
            ["OutputName"] = "templates",
            ["IntermediateOutputPath"] = EnsureTrailingSeparator(intermediatePath),
            ["ProductVersion"] = "1.0.0",
            ["BundleVersion"] = "1.0.0",
            ["Version"] = "11.0.100",
            ["DependencyKeyName"] = "NetCore_Templates_11.0",
            ["TemplateLayoutDirectoryToHarvest"] = layoutPath,
            ["BrandName"] = "Test Templates",
            ["UpgradeCode"] = "6353F5B2-B6D4-430B-81E8-D21B17F73A3C",
            ["TemplateLayoutCompletionStamp"] = completionStamp,
            ["DotnetSrc"] = layoutPath,
        };

        string coldBinlog = BuildTemplateMsi(root, projectPath, packageCache, properties, "cold", restore: true);
        AssertTargetExecution(coldBinlog, "CoreCompile", executed: true);
        AssertTargetExecution(coldBinlog, "HarvestDirectory", executed: true);
        AssertDefineConstantsContract(root, packageCache, wixVersion);

        DateTime installerTimestamp = File.GetLastWriteTimeUtc(installerPath);
        DateTime buildPropertiesTimestamp = File.GetLastWriteTimeUtc(buildPropertiesFile);

        string warmBinlog = BuildTemplateMsi(root, projectPath, packageCache, properties, "warm");
        AssertTargetExecution(warmBinlog, "CoreCompile", executed: false);
        AssertTargetExecution(warmBinlog, "HarvestDirectory", executed: false);
        File.GetLastWriteTimeUtc(installerPath).Should().Be(installerTimestamp);
        File.GetLastWriteTimeUtc(buildPropertiesFile).Should().Be(buildPropertiesTimestamp);

        properties["ProductVersion"] = "1.0.1";
        string versionBinlog = BuildTemplateMsi(root, projectPath, packageCache, properties, "product-version");
        AssertTargetExecution(versionBinlog, "CoreCompile", executed: true);
        AssertTargetExecution(versionBinlog, "HarvestDirectory", executed: false);
        ReadMsiProperties(installerPath)["ProductVersion"].Should().Be("1.0.1");
        File.GetLastWriteTimeUtc(buildPropertiesFile).Should().BeAfter(buildPropertiesTimestamp);
        AssertWarmBuild(root, projectPath, packageCache, properties, "product-version-warm");

        properties["BrandName"] = "Changed Templates";
        string brandBinlog = BuildTemplateMsi(root, projectPath, packageCache, properties, "brand");
        AssertTargetExecution(brandBinlog, "CoreCompile", executed: true);
        AssertTargetExecution(brandBinlog, "HarvestDirectory", executed: false);
        ReadMsiProperties(installerPath)["ProductName"].Should().Be("Changed Templates x64");
        AssertWarmBuild(root, projectPath, packageCache, properties, "brand-warm");

        File.WriteAllText(retainedPackage, "retained package changed");
        SetLastWriteTimeAfter(retainedPackage, File.GetLastWriteTimeUtc(installerPath));
        string editBinlog = BuildTemplateMsi(root, projectPath, packageCache, properties, "payload-edit");
        AssertTargetExecution(editBinlog, "CoreCompile", executed: true);
        AssertTargetExecution(editBinlog, "HarvestDirectory", executed: true);
        ReadMsiFiles(installerPath)["retained.nupkg"].Should().Be(new FileInfo(retainedPackage).Length);
        AssertWarmBuild(root, projectPath, packageCache, properties, "payload-edit-warm");

        File.Delete(removedPackage);
        File.WriteAllText(completionStamp, "layout changed");
        SetLastWriteTimeAfter(completionStamp, File.GetLastWriteTimeUtc(installerPath));
        string removalBinlog = BuildTemplateMsi(root, projectPath, packageCache, properties, "payload-removal");
        AssertTargetExecution(removalBinlog, "CoreCompile", executed: true);
        AssertTargetExecution(removalBinlog, "HarvestDirectory", executed: true);
        Dictionary<string, long> files = ReadMsiFiles(installerPath);
        files.Should().ContainKey("retained.nupkg");
        files.Should().NotContainKey("removed.nupkg");
        AssertWarmBuild(root, projectPath, packageCache, properties, "payload-removal-warm");
    }

    private string BuildTemplateMsi(
        string root,
        string projectPath,
        string packageCache,
        IReadOnlyDictionary<string, string> properties,
        string name,
        bool restore = false)
    {
        string binlogPath = Path.Combine(root, $"{name}.binlog");
        var arguments = new List<string>
        {
            "msbuild",
            projectPath,
            "/t:Build",
            "/nr:false",
            $"/bl:{binlogPath}",
        };

        if (restore)
        {
            arguments.Add("/restore");
        }

        arguments.AddRange(properties.Select(property => $"/p:{property.Key}={property.Value}"));

        new DotnetCommand(Log, arguments.ToArray())
            .WithWorkingDirectory(root)
            .WithEnvironmentVariable("NUGET_PACKAGES", packageCache)
            .WithEnvironmentVariable("DOTNET_MULTILEVEL_LOOKUP", "0")
            .Execute()
            .Should().Pass();

        return binlogPath;
    }

    private void AssertWarmBuild(
        string root,
        string projectPath,
        string packageCache,
        IReadOnlyDictionary<string, string> properties,
        string name)
    {
        string binlog = BuildTemplateMsi(root, projectPath, packageCache, properties, name);
        AssertTargetExecution(binlog, "CoreCompile", executed: false);
        AssertTargetExecution(binlog, "HarvestDirectory", executed: false);
    }

    private static void AssertTargetExecution(string binlogPath, string targetName, bool executed)
    {
        bool started = false;
        bool skippedAsUpToDate = false;
        var replay = new BinaryLogReplayEventSource();
        replay.AnyEventRaised += (_, args) =>
        {
            if (args is TargetStartedEventArgs targetStarted
                && string.Equals(Path.GetFileName(targetStarted.ProjectFile), "templates.wixproj", StringComparison.OrdinalIgnoreCase)
                && string.Equals(targetStarted.TargetName, targetName, StringComparison.Ordinal))
            {
                started = true;
            }
            else if (args is TargetSkippedEventArgs targetSkipped
                && string.Equals(Path.GetFileName(targetSkipped.ProjectFile), "templates.wixproj", StringComparison.OrdinalIgnoreCase)
                && string.Equals(targetSkipped.TargetName, targetName, StringComparison.Ordinal)
                && targetSkipped.SkipReason == TargetSkipReason.OutputsUpToDate)
            {
                skippedAsUpToDate = true;
            }
        };
        replay.Replay(binlogPath);

        if (executed)
        {
            started.Should().BeTrue($"{targetName} should execute in {Path.GetFileName(binlogPath)}");
        }
        else
        {
            skippedAsUpToDate.Should().BeTrue($"{targetName} should be skipped as up-to-date in {Path.GetFileName(binlogPath)}");
        }
    }

    private static void AssertDefineConstantsContract(string root, string packageCache, string wixVersion)
    {
        const string TargetName = "IncludeTemplateMsiBuildProperties";

        string projectPath = Path.Combine(root, "pkg", "windows", "msis", "templates", "templates.wixproj");
        string serializedConstants = XDocument.Load(projectPath)
            .Descendants()
            .Single(element => element.Name.LocalName == "Target" && (string?)element.Attribute("Name") == TargetName)
            .Descendants()
            .Single(element => element.Name.LocalName == "WriteLinesToFile")
            .Attribute("Lines")!
            .Value;

        string wixTargetsPath = Path.Combine(packageCache, "microsoft.wixtoolset.sdk", wixVersion, "tools", "wix.targets");
        string wixConstants = XDocument.Load(wixTargetsPath)
            .Descendants()
            .Single(element => element.Name.LocalName == "WixBuild")
            .Attribute("DefineConstants")!
            .Value;

        string layoutTargetsPath = Path.Combine(root, "pkg", "windows", "Directory.Build.targets");
        string wixpackConstants = XDocument.Load(layoutTargetsPath)
            .Descendants()
            .Single(element => element.Name.LocalName == "CreateWixBuildWixpack")
            .Attribute("DefineConstants")!
            .Value;

        serializedConstants.Should().Be(wixConstants);
        serializedConstants.Should().Be(wixpackConstants);
    }

    private static void WriteBuildEnvironment(
        string root,
        string packageFeed,
        string wixVersion,
        string installersVersion)
    {
        File.WriteAllText(Path.Combine(root, "global.json"), $$"""
            {
              "msbuild-sdks": {
                "Microsoft.WixToolset.Sdk": "{{wixVersion}}"
              }
            }
            """);

        File.WriteAllText(Path.Combine(root, "NuGet.config"), $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="wix-test-packages" value="{{SecurityElement.Escape(packageFeed)}}" />
              </packageSources>
            </configuration>
            """);

        File.WriteAllText(Path.Combine(root, "Directory.Packages.props"), $$"""
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Microsoft.WixToolset.Heat" Version="{{wixVersion}}" />
                <PackageVersion Include="Microsoft.WixToolset.Util.wixext" Version="{{wixVersion}}" />
                <PackageVersion Include="Microsoft.WixToolset.UI.wixext" Version="{{wixVersion}}" />
                <PackageVersion Include="Microsoft.WixToolset.Dependency.wixext" Version="{{wixVersion}}" />
                <PackageVersion Include="Microsoft.DotNet.Build.Tasks.Installers" Version="{{installersVersion}}" />
              </ItemGroup>
            </Project>
            """);

        File.WriteAllText(Path.Combine(root, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <ArtifactsNonShippingPackagesDir>$(MSBuildThisFileDirectory)output\</ArtifactsNonShippingPackagesDir>
                <TargetArchitecture>x64</TargetArchitecture>
                <CliProductBandVersion>11.0.1</CliProductBandVersion>
                <VersionMajor>11</VersionMajor>
                <VersionMinor>0</VersionMinor>
                <SdkBrandName>Microsoft .NET SDK 11.0.100</SdkBrandName>
                <Version>11.0.100</Version>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Combine(root, "Directory.Build.targets"), "<Project />");
    }

    private static string GetPackageVersion(string packageFeed, string packageId)
    {
        string packagePath = Directory.GetFiles(packageFeed, $"{packageId}.*.nupkg", SearchOption.TopDirectoryOnly)
            .Should().ContainSingle().Which;
        string fileName = Path.GetFileNameWithoutExtension(packagePath);
        return fileName[(packageId.Length + 1)..];
    }

    private static string CreateFile(string directory, string fileName, string contents)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, fileName);
        File.WriteAllText(path, contents);
        return path;
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

    private static void CopyDirectory(string source, string destination)
    {
        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string destinationPath = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath);
        }
    }

    private static void SetLastWriteTimeAfter(string path, DateTime timestamp)
    {
        while (File.GetLastWriteTimeUtc(path) <= timestamp)
        {
            WaitForUtcNowToAdvance();
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
    }

    private static Dictionary<string, string> ReadMsiProperties(string installerPath)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ReadMsiRows(installerPath, "SELECT `Property`, `Value` FROM `Property`", record =>
        {
            properties[(string)record[1]] = (string)record[2];
        });
        return properties;
    }

    private static Dictionary<string, long> ReadMsiFiles(string installerPath)
    {
        var files = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        ReadMsiRows(installerPath, "SELECT `FileName`, `FileSize` FROM `File`", record =>
        {
            string fileName = (string)record[1];
            int separator = fileName.IndexOf('|');
            files[separator < 0 ? fileName : fileName[(separator + 1)..]] = (int)record[2];
        });
        return files;
    }

    private static void ReadMsiRows(string installerPath, string query, Action<Record> readRow)
    {
        using var database = new Database(installerPath, DatabaseOpenMode.ReadOnly);
        using View view = database.OpenView(query);
        view.Execute();
        foreach (Record record in view)
        {
            using (record)
            {
                readRow(record);
            }
        }
    }
}
