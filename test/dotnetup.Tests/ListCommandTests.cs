// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.List;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class ListCommandTests(TestContext testContext) : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("dotnetup-list-");
    private StringWriter Output { get; } = new();

    public void Dispose()
    {
        try
        {
            testContext.WriteLine(Output.ToString());
        }
        finally
        {
            Output.Dispose();
            _tempDir.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow(new[] { "list" }, OutputFormat.Text, false)]
    [DataRow(new[] { "list", "--format", "json" }, OutputFormat.Json, false)]
    [DataRow(new[] { "list", "--no-verify" }, OutputFormat.Text, true)]
    [DataRow(new[] { "list", "--format", "json", "--no-verify" }, OutputFormat.Json, true)]
    public void Parser_ShouldParseListCommand(string[] args, OutputFormat expectedFormat, bool expectedNoVerify)
    {
        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValue(CommonOptions.FormatOption).Should().Be(expectedFormat);
        parseResult.GetValue(ListCommandParser.NoVerifyOption).Should().Be(expectedNoVerify);
    }

    [TestMethod]
    public void InstallationLister_GetInstallations_ShouldReturnList()
    {
        // Use an isolated temp manifest to avoid reading a stale/legacy manifest on CI
        string manifestPath = Path.Combine(_tempDir.FullName, "dotnetup", "manifest.json");

        // Act
        var listData = InstallationLister.GetListData(verify: false, manifestPath: manifestPath);

        // Assert
        listData.Installations.Should().NotBeNull();
        listData.Installations.Should().BeOfType<List<InstallationInfo>>();
    }

    [TestMethod]
    public void InstallationLister_WriteHumanReadable_EmptyList_ShouldOutputSummaryWithoutTitle()
    {
        // Arrange
        var listData = new ListData();

        // Act
        InstallationLister.WriteHumanReadable(Output, listData);
        var output = Output.ToString();

        // Assert
        output.Should().NotContain("managed by dotnetup");
        output.Should().Contain("0 install specs; 0 installations (0 SDKs, 0 runtimes)");
    }

    [TestMethod]
    public void InstallationLister_WriteHumanReadable_WithInstallations_ShouldShowDetails()
    {
        // Arrange
        var testInstallRoot = Path.Combine(_tempDir.FullName, ".dotnet");
        var listData = new ListData
        {
            Installations = new List<InstallationInfo>
            {
                new() { Component = InstallComponent.SDK, Version = "9.0.100", InstallRoot = testInstallRoot, Architecture = InstallArchitecture.x64 },
                new() { Component = InstallComponent.Runtime, Version = "9.0.0", InstallRoot = testInstallRoot, Architecture = InstallArchitecture.x64 }
            }
        };

        // Act
        InstallationLister.WriteHumanReadable(Output, listData);
        var output = Output.ToString();

        // Assert
        output.Should().Contain("SDK");
        output.Should().Contain("9.0.100");
        output.Should().Contain(".NET Runtime");
        output.Should().Contain("9.0.0");
        output.Should().Contain("Install spec");
        output.Should().Contain("Source");
        output.Should().Contain("0 install specs; 2 installations (1 SDK, 1 runtime)");
    }

    [TestMethod]
    public void InstallationLister_WriteJson_ShouldOutputValidJson()
    {
        // Arrange
        var listData = new ListData();

        // Act
        InstallationLister.WriteJson(Output, listData);
        var output = Output.ToString();

        // Assert
        var jsonAction = () => JsonDocument.Parse(output);
        jsonAction.Should().NotThrow();
    }

    [TestMethod]
    public void InstallationLister_WriteJson_ShouldContainExpectedStructure()
    {
        // Arrange
        var testInstallRoot = _tempDir.FullName;
        var listData = new ListData
        {
            Installations = new List<InstallationInfo>
            {
                new() { Component = InstallComponent.SDK, Version = "9.0.100", InstallRoot = testInstallRoot, Architecture = InstallArchitecture.x64 }
            }
        };

        // Act
        InstallationLister.WriteJson(Output, listData);
        var output = Output.ToString();

        // Assert
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        root.TryGetProperty("installations", out var installationsArray).Should().BeTrue();
        installationsArray.GetArrayLength().Should().Be(1);

        var firstInstall = installationsArray[0];
        firstInstall.GetProperty("component").GetString().Should().Be("SDK");
        firstInstall.GetProperty("version").GetString().Should().Be("9.0.100");
        firstInstall.GetProperty("installRoot").GetString().Should().Be(testInstallRoot);
        firstInstall.GetProperty("architecture").GetString().Should().Be("x64");
    }

    [TestMethod]
    public void InstallationLister_WriteJson_EmptyList_ShouldHaveEmptyInstallations()
    {
        // Arrange
        var listData = new ListData();

        // Act
        InstallationLister.WriteJson(Output, listData);
        var output = Output.ToString();

        // Assert
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        root.GetProperty("installations").GetArrayLength().Should().Be(0);
    }

    [TestMethod]
    public void RelationshipRows_ShowSharedInstallationsAndUnreferencedVersions()
    {
        var installations = new List<InstallationInfo>
        {
            Installation("10.0.105"), Installation("10.0.103"), Installation("9.0.304")
        };
        var specs = new[] { Spec("10.0.1xx"), Spec("10.0"), Spec("10.0.103"), Spec("11.0.1xx") };

        var rows = InstallationListRenderer.CreateRows(specs, installations);

        rows.Select(r => r.SpecDisplay).Should().Equal("10.0", "10.0.1xx", "10.0.103", "-", "11.0.1xx");
        rows.Select(r => r.Installation?.Version).Should().Equal("10.0.105", "10.0.105", "10.0.103", "9.0.304", null);
        rows[0].SourceDisplay.Should().Be("Command line");
        rows[3].SourceDisplay.Should().Be("-");
        rows[3].Spec.Should().BeNull();
        rows[4].Error.Should().BeNull();

        InstallationListRenderer.Write(Output, new ListData { InstallSpecs = [.. specs], Installations = installations }, width: 160);
        Output.ToString().Should().Contain("4 install specs; 3 installations (3 SDKs, 0 runtimes)");
        Output.ToString().Should().NotContain("Tracked channels").And.NotContain("Installed versions:");
    }

    [TestMethod]
    public void RelationshipRows_SortRuntimesTogetherBySemanticVersionThenComponent()
    {
        var installations = new List<InstallationInfo>
        {
            Installation("9.0.9", InstallComponent.Runtime),
            Installation("10.0.2", InstallComponent.WindowsDesktop),
            Installation("10.0.10", InstallComponent.ASPNETCore),
            Installation("10.0.10-preview.2", InstallComponent.Runtime),
            Installation("10.0.10", InstallComponent.Runtime),
            Installation("9.0.100")
        };
        var specs = new[]
        {
            Spec("12.0.1xx"), Spec("13.0", InstallComponent.WindowsDesktop), Spec("12.0", InstallComponent.Runtime)
        };

        var rows = InstallationListRenderer.CreateRows(specs, installations);

        rows.Select(r => r.Installation?.Version).Should()
            .Equal("9.0.100", null, "10.0.10", "10.0.10", "10.0.10-preview.2", "10.0.2", "9.0.9", null, null);
        rows.Select(r => r.Component).Should().Equal(
            InstallComponent.SDK, InstallComponent.SDK, InstallComponent.Runtime, InstallComponent.ASPNETCore,
            InstallComponent.Runtime, InstallComponent.WindowsDesktop, InstallComponent.Runtime,
            InstallComponent.Runtime, InstallComponent.WindowsDesktop);
    }

    [TestMethod]
    [DataRow("latest", "10.0.105")]
    [DataRow("lts", "10.0.105")]
    [DataRow("preview", "11.0.100-preview.2")]
    [DataRow("11.0-daily", "11.0.100-preview.2")]
    [DataRow("10.0.103", null)]
    public void RelationshipRows_UseExistingChannelMatching(string channel, string? expected)
    {
        var spec = Spec(channel);
        var rows = InstallationListRenderer.CreateRows([spec],
            [Installation("9.0.304"), Installation("10.0.105"), Installation("11.0.100-preview.2")]);

        (rows.Single(r => r.Spec == spec).Installation?.Version).Should().Be(expected);
    }

    [TestMethod]
    public void RelationshipTable_DistinguishesMigrationAndCommandLineSources()
    {
        var migrated = Spec("10.0.1xx");
        migrated.Source = InstallSource.Migration;
        var data = new ListData
        {
            InstallSpecs = [migrated, Spec("10.0.1xx")],
            Installations = [Installation("10.0.105")]
        };

        var rows = InstallationListRenderer.CreateRows(data.InstallSpecs, data.Installations);
        rows.Select(r => r.SourceDisplay).Should().Equal("Command line", "Migration");
        rows.Should().OnlyContain(r => r.Installation == data.Installations[0]);
        InstallationListRenderer.Write(Output, data, width: 160);
        Output.ToString().Should().Contain("Migration").And.Contain("Command line")
            .And.Contain("2 install specs; 1 installation (1 SDK, 0 runtimes)");

        var json = JsonSerializer.Serialize(data, InstallationListJsonContext.Default.ListData);
        json.Should().Contain("\"Migration\"").And.Contain("\"Explicit\"");
    }

    [TestMethod]
    public void RelationshipRows_DoNotResolveAcrossRootsOrArchitectures()
    {
        var spec = Spec("10.0.1xx");
        spec.InstallRoot = Path.Combine(_tempDir.FullName, "root-one");
        spec.Architecture = InstallArchitecture.x64;
        var otherRoot = Installation("10.0.105");
        otherRoot.InstallRoot = Path.Combine(_tempDir.FullName, "root-two");
        otherRoot.Architecture = InstallArchitecture.x64;
        var otherArchitecture = Installation("10.0.105");
        otherArchitecture.InstallRoot = spec.InstallRoot;
        otherArchitecture.Architecture = InstallArchitecture.arm64;
        var data = new ListData { InstallSpecs = [spec], Installations = [otherRoot, otherArchitecture] };

        InstallationListRenderer.CreateRows(data.InstallSpecs, data.Installations).Single(r => r.Spec == spec)
            .Installation.Should().BeNull();
        InstallationListRenderer.Write(Output, data, width: 160);
        Output.ToString().Should().Contain($"{spec.InstallRoot} (x64)")
            .And.Contain($"{spec.InstallRoot} (arm64)").And.Contain($"{otherRoot.InstallRoot} (x64)")
            .And.Contain("1 install spec; 2 installations (2 SDKs, 0 runtimes)");
    }

    [TestMethod]
    public void RelationshipRows_ReadCurrentGlobalJsonAndReportMalformedFiles()
    {
        var path = Path.Combine(_tempDir.FullName, "global.json");
        var spec = Spec("9.0.1xx");
        spec.Source = InstallSource.GlobalJson;
        spec.GlobalJsonPath = path;
        var installations = new List<InstallationInfo> { Installation("10.0.103"), Installation("10.0.105") };
        File.WriteAllText(path, """{"sdk":{"version":"10.0.103","rollForward":"disable","allowPrerelease":false}}""");

        var rows = InstallationListRenderer.CreateRows([spec], installations);
        var row = rows.Single(r => r.Spec == spec);
        row.SpecDisplay.Should().Be("10.0.103 (rollForward: disable, allowPrerelease: false)");
        row.SourceDisplay.Should().Be(path);
        row.Installation.Should().BeSameAs(installations[0]);
        spec.VersionOrChannel.Should().Be("9.0.1xx", "rendering must not change the manifest snapshot");

        File.WriteAllText(path, "{broken");
        rows = InstallationListRenderer.CreateRows([spec], installations);
        row = rows.Last();
        row.Spec.Should().BeSameAs(spec);
        row.SpecDisplay.Should().Be("Unavailable");
        row.Installation.Should().BeNull();
        row.Error.Should().Contain(path);
        rows.Count(r => r.Spec is null).Should().Be(2);

        InstallationListRenderer.Write(Output, new ListData { InstallSpecs = [spec], Installations = installations }, width: 200);
        Output.ToString().Should().Contain("Unknown").And.Contain("Warning: Cannot read");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RelationshipRows_OmitDeletedOrVersionlessGlobalJson(bool exists)
    {
        var spec = Spec("10.0.1xx");
        spec.Source = InstallSource.GlobalJson;
        spec.GlobalJsonPath = Path.Combine(_tempDir.FullName, "global.json");
        if (exists)
        {
            File.WriteAllText(spec.GlobalJsonPath, "{}");
        }

        var row = InstallationListRenderer.CreateRows([spec], [Installation("10.0.105")]).Single();
        row.Spec.Should().BeNull();
        row.SpecDisplay.Should().Be("-");
        row.SourceDisplay.Should().Be("-");
        row.Installation.Should().NotBeNull();
    }

    [TestMethod]
    public void RelationshipTable_PreservesLiteralMarkupAndValidationDetailsWhenWrapping()
    {
        var spec = Spec("[bold]10.0[/]");
        var installation = Installation("10.0.105");
        installation.IsValid = false;
        installation.ValidationFailure = "Missing [runtime] files";
        var data = new ListData { InstallSpecs = [spec], Installations = [installation] };

        InstallationListRenderer.Write(Output, data, width: 70);
        var output = Output.ToString();
        var withoutWhitespace = string.Concat(output.Where(c => !char.IsWhiteSpace(c)));
        withoutWhitespace.Should().Contain("[bold]10.0[/]").And.Contain("10.0.105(invalid)");
        output.Should().Contain("Warning: SDK 10.0.105 is invalid: Missing [runtime] files");
    }

    [TestMethod]
    public void InstallationLister_WriteJson_PreservesSpecAndInstallationContracts()
    {
        var spec = Spec("10.0.1xx");
        spec.Source = InstallSource.GlobalJson;
        spec.GlobalJsonPath = "missing-global.json";
        var installation = Installation("10.0.105");
        var data = new ListData { InstallSpecs = [spec], Installations = [installation] };
        InstallationLister.WriteJson(Output, data);

        using var json = JsonDocument.Parse(Output.ToString());
        json.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal("installSpecs", "installations");
        var jsonSpec = json.RootElement.GetProperty("installSpecs")[0];
        jsonSpec.EnumerateObject().Select(p => p.Name).Should()
            .Equal("component", "versionOrChannel", "source", "globalJsonPath", "installRoot", "architecture");
        jsonSpec.GetProperty("source").GetString().Should().Be("GlobalJson");
        jsonSpec.GetProperty("versionOrChannel").GetString().Should().Be("10.0.1xx");
        json.RootElement.GetProperty("installations")[0].EnumerateObject().Select(p => p.Name).Should()
            .Equal("component", "version", "installRoot", "architecture", "frameworkName");
    }

    [TestMethod]
    public void RelationshipRows_UnreadableGlobalJsonDoesNotUseCachedChannel()
    {
        var spec = Spec("10.0.1xx");
        spec.Source = InstallSource.GlobalJson;
        spec.GlobalJsonPath = Path.Combine(_tempDir.FullName, "global.json");
        File.WriteAllText(spec.GlobalJsonPath, """{"sdk":{"version":"10.0.100"}}""");
        using var locked = File.Open(spec.GlobalJsonPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var rows = InstallationListRenderer.CreateRows([spec], [Installation("10.0.105")]);

        rows.Single(r => r.Spec is null).Installation!.Version.Should().Be("10.0.105");
        var repositoryRow = rows.Single(r => r.Spec is not null);
        repositoryRow.SpecDisplay.Should().Be("Unavailable");
        repositoryRow.Installation.Should().BeNull();
        repositoryRow.Error.Should().Contain(spec.GlobalJsonPath);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InstallationLister_ListsOnlyTopLevelInstallations(bool verify)
    {
        var root = _tempDir.FullName;
        Directory.CreateDirectory(Path.Combine(root, "sdk", "10.0.105"));
        Directory.CreateDirectory(Path.Combine(root, "shared", InstallComponentExtensions.RuntimeFrameworkName, "10.0.5"));
        var manifestPath = Path.Combine(root, "manifest.json");
        var data = new DotnetupManifestData
        {
            DotnetRoots =
            [
                new()
                {
                    Path = root,
                    Architecture = InstallArchitecture.x64,
                    InstallSpecs = [new() { Component = InstallComponent.SDK, VersionOrChannel = "10.0.1xx" }],
                    Installations =
                    [
                        new() { Component = InstallComponent.SDK, Version = "10.0.105",
                            Subcomponents = ["sdk/10.0.105", "shared/Microsoft.NETCore.App/10.0.5"] }
                    ]
                }
            ]
        };
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            new DotnetupSharedManifest(manifestPath).WriteManifest(data);
        }

        var listed = InstallationLister.GetListData(verify, manifestPath);
        listed.Installations.Should().ContainSingle().Which.Component.Should().Be(InstallComponent.SDK);
        listed.Installations[0].IsValid.Should().Be(verify ? false : null);
        InstallationListRenderer.CreateRows(listed.InstallSpecs, listed.Installations).Should().ContainSingle()
            .Which.Installation.Should().NotBeNull();

        data.DotnetRoots[0].Installations.Add(new()
        {
            Component = InstallComponent.Runtime, Version = "10.0.5", Subcomponents = ["shared/Microsoft.NETCore.App/10.0.5"]
        });
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            new DotnetupSharedManifest(manifestPath).WriteManifest(data);
        }
        listed = InstallationLister.GetListData(verify, manifestPath);
        listed.Installations.Select(i => i.Component).Should().Equal(InstallComponent.SDK, InstallComponent.Runtime);
    }

    private InstallationInfo Installation(string version, InstallComponent component = InstallComponent.SDK) =>
        new() { Component = component, Version = version, InstallRoot = _tempDir.FullName };

    private InstallSpecInfo Spec(string version, InstallComponent component = InstallComponent.SDK) =>
        new() { Component = component, VersionOrChannel = version, Source = InstallSource.Explicit, InstallRoot = _tempDir.FullName };
}
