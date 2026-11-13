// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Strings = Microsoft.DotNet.Tools.Bootstrapper.Strings;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class GlobalJsonUpdateTests : IDisposable
{
    private readonly string _testDir;

    public GlobalJsonUpdateTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dotnetup-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  ReplaceGlobalJsonSdkVersion (pure string→string transform)
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void ReplaceGlobalJsonSdkVersion_ReplacesVersionInSdkSection()
    {
        var json = """
            {
              "sdk": {
                "version": "8.0.100"
              }
            }
            """;

        var result = GlobalJsonHelper.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

        result.Should().NotBeNull();
        result.Should().Contain("\"10.0.100\"");
        result.Should().NotContain("\"8.0.100\"");
    }

    [TestMethod]
    public void ReplaceGlobalJsonSdkVersion_PreservesFormatting()
    {
        // Intentionally irregular whitespace
        var json = "{\n  \"sdk\" :  { \"version\" : \"8.0.100\" ,  \"rollForward\": \"latestFeature\" }\n}";

        var result = GlobalJsonHelper.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

        result.Should().NotBeNull();
        // Everything except the version value should remain unchanged
        result.Should().Contain("\"rollForward\": \"latestFeature\"");
        result.Should().Contain("\"10.0.100\"");
    }

    [TestMethod]
    public void ReplaceGlobalJsonSdkVersion_PreservesOtherProperties()
    {
        var json = """
            {
              "sdk": {
                "version": "8.0.100",
                "rollForward": "latestFeature",
                "allowPrerelease": false
              },
              "tools": {
                "dotnet": "8.0.100"
              }
            }
            """;

        var result = GlobalJsonHelper.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

        result.Should().NotBeNull();
        result.Should().Contain("\"10.0.100\"");
        result.Should().Contain("\"rollForward\": \"latestFeature\"");
        result.Should().Contain("\"allowPrerelease\": false");
        // The "dotnet" value in tools should NOT be changed
        result.Should().Contain("\"dotnet\": \"8.0.100\"");
    }

    [TestMethod]
    public void ReplaceGlobalJsonSdkVersion_ReturnsNull_WhenNoSdkSection()
    {
        var json = """
            {
              "tools": {
                "version": "8.0.100"
              }
            }
            """;

        var result = GlobalJsonHelper.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

        result.Should().BeNull();
    }

    [TestMethod]
    public void ReplaceGlobalJsonSdkVersion_ReturnsNull_WhenNoVersionInSdk()
    {
        var json = """
            {
              "sdk": {
                "rollForward": "latestFeature"
              }
            }
            """;

        var result = GlobalJsonHelper.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

        result.Should().BeNull();
    }

    [TestMethod]
    public void ReplaceGlobalJsonSdkVersion_HandlesVersionPropertyOutsideSdk()
    {
        // A "version" property at the root level should NOT be replaced
        var json = """
            {
              "version": "1.0.0",
              "sdk": {
                "rollForward": "latestFeature"
              }
            }
            """;

        var result = GlobalJsonHelper.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

        result.Should().BeNull();
    }

    [TestMethod]
    public void ReplaceGlobalJsonSdkVersion_ReplacesLongerVersion()
    {
        var json = """
            {
              "sdk": {
                "version": "8.0.100"
              }
            }
            """;

        var result = GlobalJsonHelper.ReplaceGlobalJsonSdkVersion(json, "10.0.100-preview.1");

        result.Should().NotBeNull();
        result.Should().Contain("\"10.0.100-preview.1\"");
    }

    [TestMethod]
    public void ReplaceGlobalJsonSdkVersion_ReplacesShorterVersion()
    {
        var json = """
            {
              "sdk": {
                "version": "10.0.100-preview.1.12345"
              }
            }
            """;

        var result = GlobalJsonHelper.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

        result.Should().NotBeNull();
        result.Should().Contain("\"10.0.100\"");
        result.Should().NotContain("preview");
    }

    // ──────────────────────────────────────────────────────────────
    //  UpdateGlobalJson (file I/O layer)
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void UpdateGlobalJson_DoesNothing_WhenSdkVersionIsNull()
    {
        var path = Path.Combine(_testDir, "global.json");
        var original = """{ "sdk": { "version": "8.0.100" } }""";
        File.WriteAllText(path, original);

        GlobalJsonHelper.UpdateGlobalJson(path, sdkVersion: null);

        File.ReadAllText(path).Should().Be(original);
    }

    [TestMethod]
    public void UpdateGlobalJson_DoesNothing_WhenFileDoesNotExist()
    {
        var path = Path.Combine(_testDir, "nonexistent", "global.json");

        // Should not throw
        var ex = Record.Exception(() => GlobalJsonHelper.UpdateGlobalJson(path, sdkVersion: "10.0.100"));
        ex.Should().BeNull();
    }

    [TestMethod]
    public void UpdateGlobalJson_UpdatesVersionInFile()
    {
        var path = Path.Combine(_testDir, "global.json");
        File.WriteAllText(path, """{ "sdk": { "version": "8.0.100" } }""");

        GlobalJsonHelper.UpdateGlobalJson(path, sdkVersion: "10.0.100");

        var updated = File.ReadAllText(path);
        updated.Should().Contain("\"10.0.100\"");
        updated.Should().NotContain("\"8.0.100\"");
    }

    [TestMethod]
    public void UpdateGlobalJson_DoesNotWriteFile_WhenNoSdkVersionToReplace()
    {
        var path = Path.Combine(_testDir, "global.json");
        var original = """{ "tools": { "dotnet": "8.0.100" } }""";
        File.WriteAllText(path, original);
        var lastWrite = File.GetLastWriteTimeUtc(path);

        // Small delay so any write would be detectable
        Thread.Sleep(50);

        GlobalJsonHelper.UpdateGlobalJson(path, sdkVersion: "10.0.100");

        File.ReadAllText(path).Should().Be(original);
        File.GetLastWriteTimeUtc(path).Should().Be(lastWrite);
    }

    [TestMethod]
    [DataRow("11.0.100", "\"11.0.100\"")]
    [DataRow(null, "\"11.0.100-rtm.26552.115\"")]
    public void ApplyGlobalJsonModifications_WritesInstalledLayoutVersion(string? payloadVersion, string expected)
    {
        var installRoot = new DotnetInstallRoot(Path.Combine(_testDir, "dotnet"), InstallerUtilities.GetDefaultInstallArchitecture());
        var buildVersion = new ReleaseVersion("11.0.100-rtm.26552.115");
        var path = Path.Combine(_testDir, "global.json");
        File.WriteAllText(path, """{ "sdk": { "version": "10.0.100", "rollForward": "disable" } }""");
        var request = new DotnetInstallRequest(installRoot, new UpdateChannel("11-daily"), InstallComponent.SDK,
            new InstallRequestOptions { GlobalJsonPath = path });
        var install = new DotnetInstall(installRoot, buildVersion, InstallComponent.SDK)
            .WithProductVersion(payloadVersion is null ? null : new ReleaseVersion(payloadVersion));

        new DotnetEnvironmentManager().ApplyGlobalJsonModifications(
            [new ResolvedInstallRequest(request, buildVersion)], [new InstallResult(install, WasAlreadyInstalled: false)]);

        File.ReadAllText(path).Should().Contain(expected);
    }

    // ──────────────────────────────────────────────────────────────
    //  Default behavior: --update-global-json is NOT on by default
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void UpdateGlobalJsonOption_DefaultIsNull()
    {
        // The option default must be null (tri-state: null = not specified,
        // true = explicitly requested, false = explicitly declined).
        // This ensures install/update commands do NOT update global.json
        // unless the user explicitly passes --update-global-json.
        var parseResult = Parser.Parse(["sdk", "install", "10.0"]);
        parseResult.Errors.Should().BeEmpty();

        var value = parseResult.GetValue(
            SdkInstallCommandParser.UpdateGlobalJsonOption);

        value.Should().BeNull("--update-global-json should not be set by default");
    }

    [TestMethod]
    public void UpdateGlobalJsonOption_TrueWhenExplicitlyPassed()
    {
        var parseResult = Parser.Parse(["sdk", "install", "10.0", "--update-global-json"]);
        parseResult.Errors.Should().BeEmpty();

        var value = parseResult.GetValue(
            SdkInstallCommandParser.UpdateGlobalJsonOption);

        value.Should().Be(true);
    }

    // ──────────────────────────────────────────────────────────────
    //  UTF-8 input and BOM preservation
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GetGlobalJsonInfo_ReadsCorrectVersion_WithUtf8(bool emitBom)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: emitBom);
        var json = """
            {
              "sdk": {
                "version": "9.0.200"
              }
            }
            """;
        var path = Path.Combine(_testDir, "global.json");
        File.WriteAllText(path, json, encoding);

        var info = GlobalJsonHelper.GetGlobalJsonInfo(_testDir);

        info.GlobalJsonContents.Should().NotBeNull();
        info.GlobalJsonContents!.Sdk.Should().NotBeNull();
        info.GlobalJsonContents.Sdk!.Version.Should().Be("9.0.200");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GetGlobalJsonInfo_AndChannelResolver_ReadTheSameSettings(bool emitBom)
    {
        var json = """
            {
              // SDK policy
              "sdk": {
                "version": "10.0.103",
                "rollForward": "latestPatch",
                "allowPrerelease": false,
                "paths": [".dotnet"]
              }
            }
            """;
        var path = Path.Combine(_testDir, "global.json");
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: emitBom));
        var childDirectory = Directory.CreateDirectory(Path.Combine(_testDir, "child"));

        var info = GlobalJsonHelper.GetGlobalJsonInfo(childDirectory.FullName);
        var sdk = GlobalJsonChannelResolver.ReadSdkSection(path);

        info.GlobalJsonPath.Should().Be(path);
        info.GlobalJsonContents!.Sdk.Should().BeEquivalentTo(sdk);
        info.SdkVersion.Should().Be("10.0.103");
        info.RollForward.Should().Be("latestPatch");
        info.AllowPrerelease.Should().BeFalse();
        info.SdkPath.Should().Be(Path.Combine(_testDir, ".dotnet"));

        GlobalJsonHelper.UpdateGlobalJsonIfNewer(path, new ReleaseVersion("10.0.102")).Should().BeFalse();
        File.ReadAllText(path).Should().Be(json);
        GlobalJsonHelper.UpdateGlobalJsonIfNewer(path, new ReleaseVersion("10.0.105")).Should().BeTrue();
        File.ReadAllText(path).Should().Be(json.Replace("10.0.103", "10.0.105", StringComparison.Ordinal));
        File.ReadAllBytes(path).AsSpan().StartsWith(Encoding.UTF8.GetPreamble()).Should().Be(emitBom);
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("null")]
    [DataRow("""{"sdk":[]}""")]
    [DataRow("""{"sdk":{"allowPrerelease":"false"}}""")]
    public void GetGlobalJsonInfo_ReportsMalformedContentsAsContextResolutionFailure(string contents)
    {
        var path = Path.Combine(_testDir, "global.json");
        File.WriteAllText(path, contents);

        var read = () => GlobalJsonHelper.GetGlobalJsonInfo(_testDir);

        var error = read.Should().Throw<DotnetInstallException>().Which;
        error.ErrorCode.Should().Be(DotnetInstallErrorCode.ContextResolutionFailed);
        error.Message.Should().Contain(path);
        error.InnerException.Should().BeAssignableTo<JsonException>();
    }

    [TestMethod]
    [DataRow("""{"sdk":{"version":"10.0.103"},"broken":}""")]
    [DataRow("null")]
    [DataRow("""{"sdk":[]}""")]
    [DataRow("""{"sdk":{"version":103}}""")]
    [DataRow("""{"sdk":{"version":"10.0.103","allowPrerelease":"false"}}""")]
    public void GlobalJsonUpdates_ReportMalformedContentsWithoutChangingFile(string contents)
    {
        var path = Path.Combine(_testDir, "global.json");
        File.WriteAllText(path, contents);

        var update = () => GlobalJsonHelper.UpdateGlobalJsonIfNewer(path, new ReleaseVersion("10.0.105"));
        var unconditionalUpdate = () => GlobalJsonHelper.UpdateGlobalJson(path, "10.0.105");

        update.Should().Throw<JsonException>();
        unconditionalUpdate.Should().Throw<JsonException>();
        File.ReadAllText(path).Should().Be(contents);
    }

    [TestMethod]
    [DataRow("""{"sdk":{"version":"10.0.100","version":"10.0.103"}}""")]
    [DataRow("""{"sdk":{"version":"10.0.103","version":"10.0.103"}}""")]
    [DataRow("""{"sdk":{"version":"10.0.100","\u0076ersion":"10.0.103"}}""")]
    [DataRow("""{"sdk":{"version":"10.0.100"},"sdk":{"version":"10.0.103"}}""")]
    public void GlobalJsonUpdates_RejectDuplicateSdkVersionsWithoutChangingFile(string contents)
    {
        var path = Path.Combine(_testDir, "global.json");
        File.WriteAllText(path, contents);
        var originalBytes = File.ReadAllBytes(path);

        var update = () => GlobalJsonHelper.UpdateGlobalJson(path, "10.0.105");
        var updateIfNewer = () => GlobalJsonHelper.UpdateGlobalJsonIfNewer(path, new ReleaseVersion("10.0.105"));
        var replace = () => GlobalJsonHelper.ReplaceGlobalJsonSdkVersion(contents, "10.0.105");

        update.Should().Throw<JsonException>().WithMessage(Strings.GlobalJsonDuplicateSdkVersion);
        updateIfNewer.Should().Throw<JsonException>().WithMessage(Strings.GlobalJsonDuplicateSdkVersion);
        replace.Should().Throw<JsonException>().WithMessage(Strings.GlobalJsonDuplicateSdkVersion);
        File.ReadAllBytes(path).Should().Equal(originalBytes);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("""{"sdk":{"rollForward":"latestFeature"}}""")]
    public void UpdateGlobalJsonIfNewer_ReturnsFalseWhenNoVersionCanBeReplaced(string contents)
    {
        var path = Path.Combine(_testDir, "global.json");
        File.WriteAllText(path, contents);

        GlobalJsonHelper.UpdateGlobalJsonIfNewer(path, new ReleaseVersion("10.0.105")).Should().BeFalse();

        File.ReadAllText(path).Should().Be(contents);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UpdateGlobalJson_PreservesUtf8Bom(bool emitBom)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: emitBom);
        var json = """
            {
              "sdk": {
                "version": "8.0.100"
              }
            }
            """;
        var path = Path.Combine(_testDir, "global.json");
        File.WriteAllText(path, json, encoding);

        GlobalJsonHelper.UpdateGlobalJson(path, sdkVersion: "10.0.100");

        var content = File.ReadAllText(path);
        content.Should().Contain("\"10.0.100\"");
        content.Should().NotContain("\"8.0.100\"");
        File.ReadAllBytes(path).AsSpan().StartsWith(Encoding.UTF8.GetPreamble()).Should().Be(emitBom);
    }

    [TestMethod]
    [DataRow(1200, false)]
    [DataRow(1200, true)]
    [DataRow(1201, false)]
    [DataRow(1201, true)]
    [DataRow(12000, false)]
    [DataRow(12000, true)]
    [DataRow(12001, false)]
    [DataRow(12001, true)]
    public void GlobalJson_RejectsNonUtf8WithoutChangingFile(int codePage, bool emitBom)
    {
        var encoding = Encoding.GetEncoding(codePage);
        var path = Path.Combine(_testDir, "global.json");
        byte[] contents = [..(emitBom ? encoding.GetPreamble() : []), ..encoding.GetBytes("""{"sdk":{"version":"10.0.103"}}""")];
        File.WriteAllBytes(path, contents);

        var read = () => GlobalJsonHelper.GetGlobalJsonInfo(_testDir);
        var readSdk = () => GlobalJsonChannelResolver.ReadSdkSection(path);
        var update = () => GlobalJsonHelper.UpdateGlobalJson(path, "10.0.105");
        var updateIfNewer = () => GlobalJsonHelper.UpdateGlobalJsonIfNewer(path, new ReleaseVersion("10.0.105"));

        read.Should().Throw<DotnetInstallException>()
            .Which.ErrorCode.Should().Be(DotnetInstallErrorCode.ContextResolutionFailed);
        readSdk.Should().Throw<JsonException>();
        update.Should().Throw<JsonException>();
        updateIfNewer.Should().Throw<JsonException>();
        File.ReadAllBytes(path).Should().Equal(contents);
    }

    [TestMethod]
    [DataRow("""{"sdk":{"version":"10.0.103",}}""")]
    [DataRow("""{"sdk":{"version":"10.0.103"},}""")]
    [DataRow("""{"sdk":{"paths":[".dotnet",],"version":"10.0.103"}}""")]
    [DataRow("""{"sdk":{"version":"10.0.103","paths":[".dotnet",]}}""")]
    public void GlobalJson_RejectsTrailingCommasWithoutChangingFile(string contents)
    {
        var path = Path.Combine(_testDir, "global.json");
        File.WriteAllText(path, contents);

        var read = () => GlobalJsonHelper.GetGlobalJsonInfo(_testDir);
        var readSdk = () => GlobalJsonChannelResolver.ReadSdkSection(path);
        var update = () => GlobalJsonHelper.UpdateGlobalJson(path, "10.0.105");
        var updateIfNewer = () => GlobalJsonHelper.UpdateGlobalJsonIfNewer(path, new ReleaseVersion("10.0.105"));
        var replace = () => GlobalJsonHelper.ReplaceGlobalJsonSdkVersion(contents, "10.0.105");

        read.Should().Throw<DotnetInstallException>()
            .Which.ErrorCode.Should().Be(DotnetInstallErrorCode.ContextResolutionFailed);
        readSdk.Should().Throw<JsonException>();
        update.Should().Throw<JsonException>();
        updateIfNewer.Should().Throw<JsonException>();
        replace.Should().Throw<JsonException>();
        File.ReadAllText(path).Should().Be(contents);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GlobalJson_ReadAndUpdate_PreservesNonAsciiTextAndBom(bool emitBom)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: emitBom);
        var json = "{\"sdk\":{\"version\":\"10.0.103\"},\"label\":\"\u00e9\"}";
        var path = Path.Combine(_testDir, "global.json");
        File.WriteAllText(path, json, encoding);

        GlobalJsonHelper.ReadGlobalJson(path).Sdk!.Version.Should().Be("10.0.103");
        GlobalJsonHelper.UpdateGlobalJson(path, "10.0.105");

        File.ReadAllBytes(path).Should().Equal(
            [..encoding.GetPreamble(), ..encoding.GetBytes(json.Replace("10.0.103", "10.0.105"))]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GlobalJson_RejectsInvalidUtf8WithoutChangingFile(bool emitBom)
    {
        var path = Path.Combine(_testDir, "global.json");
        byte[] contents = [..(emitBom ? Encoding.UTF8.GetPreamble() : []),
            ..Encoding.UTF8.GetBytes("{\"sdk\":{\"version\":\"10.0.103\"},\"label\":\""),
            0xff, ..Encoding.UTF8.GetBytes("\"}")];
        File.WriteAllBytes(path, contents);

        var readFile = () => GlobalJsonHelper.ReadGlobalJson(path);
        var read = () => GlobalJsonHelper.GetGlobalJsonInfo(_testDir);
        var readSdk = () => GlobalJsonChannelResolver.ReadSdkSection(path);
        var update = () => GlobalJsonHelper.UpdateGlobalJson(path, "10.0.105");
        var updateIfNewer = () => GlobalJsonHelper.UpdateGlobalJsonIfNewer(path, new ReleaseVersion("10.0.105"));

        readFile.Should().Throw<JsonException>().Which.InnerException.Should().BeOfType<DecoderFallbackException>();
        read.Should().Throw<DotnetInstallException>()
            .Which.ErrorCode.Should().Be(DotnetInstallErrorCode.ContextResolutionFailed);
        readSdk.Should().Throw<JsonException>();
        update.Should().Throw<JsonException>();
        updateIfNewer.Should().Throw<JsonException>();
        File.ReadAllBytes(path).Should().Equal(contents);
    }
}
