// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;

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

        var result = GlobalJsonModifier.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

        result.Should().NotBeNull();
        result.Should().Contain("\"10.0.100\"");
        result.Should().NotContain("\"8.0.100\"");
    }

    [TestMethod]
    public void ReplaceGlobalJsonSdkVersion_PreservesFormatting()
    {
        // Intentionally irregular whitespace
        var json = "{\n  \"sdk\" :  { \"version\" : \"8.0.100\" ,  \"rollForward\": \"latestFeature\" }\n}";

        var result = GlobalJsonModifier.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

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

        var result = GlobalJsonModifier.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

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

        var result = GlobalJsonModifier.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

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

        var result = GlobalJsonModifier.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

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

        var result = GlobalJsonModifier.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

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

        var result = GlobalJsonModifier.ReplaceGlobalJsonSdkVersion(json, "10.0.100-preview.1");

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

        var result = GlobalJsonModifier.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

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

        GlobalJsonModifier.UpdateGlobalJson(path, sdkVersion: null);

        File.ReadAllText(path).Should().Be(original);
    }

    [TestMethod]
    public void UpdateGlobalJson_DoesNothing_WhenFileDoesNotExist()
    {
        var path = Path.Combine(_testDir, "nonexistent", "global.json");

        // Should not throw
        var ex = Record.Exception(() => GlobalJsonModifier.UpdateGlobalJson(path, sdkVersion: "10.0.100"));
        ex.Should().BeNull();
    }

    [TestMethod]
    public void UpdateGlobalJson_UpdatesVersionInFile()
    {
        var path = Path.Combine(_testDir, "global.json");
        File.WriteAllText(path, """{ "sdk": { "version": "8.0.100" } }""");

        GlobalJsonModifier.UpdateGlobalJson(path, sdkVersion: "10.0.100");

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

        GlobalJsonModifier.UpdateGlobalJson(path, sdkVersion: "10.0.100");

        File.ReadAllText(path).Should().Be(original);
        File.GetLastWriteTimeUtc(path).Should().Be(lastWrite);
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

        var info = GlobalJsonModifier.GetGlobalJsonInfo(_testDir);

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

        var info = GlobalJsonModifier.GetGlobalJsonInfo(childDirectory.FullName);
        var sdk = GlobalJsonChannelResolver.ReadSdkSection(path);

        info.GlobalJsonPath.Should().Be(path);
        info.GlobalJsonContents!.Sdk.Should().BeEquivalentTo(sdk);
        info.SdkVersion.Should().Be("10.0.103");
        info.RollForward.Should().Be("latestPatch");
        info.AllowPrerelease.Should().BeFalse();
        info.SdkPath.Should().Be(Path.Combine(_testDir, ".dotnet"));

        GlobalJsonModifier.UpdateGlobalJsonIfNewer(path, new ReleaseVersion("10.0.102")).Should().BeFalse();
        File.ReadAllText(path).Should().Be(json);
        GlobalJsonModifier.UpdateGlobalJsonIfNewer(path, new ReleaseVersion("10.0.105")).Should().BeTrue();
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

        var read = () => GlobalJsonModifier.GetGlobalJsonInfo(_testDir);

        var error = read.Should().Throw<DotnetInstallException>().Which;
        error.ErrorCode.Should().Be(DotnetInstallErrorCode.ContextResolutionFailed);
        error.Message.Should().Contain(path);
        error.InnerException.Should().BeAssignableTo<JsonException>();
    }

    [TestMethod]
    public void UpdateGlobalJsonIfNewer_ReportsMalformedContentsWithoutChangingFile()
    {
        var path = Path.Combine(_testDir, "global.json");
        var contents = """{"sdk":{"version":"10.0.103"},"broken":}""";
        File.WriteAllText(path, contents);

        var update = () => GlobalJsonModifier.UpdateGlobalJsonIfNewer(path, new ReleaseVersion("10.0.105"));

        update.Should().Throw<JsonException>();
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

        GlobalJsonModifier.UpdateGlobalJson(path, sdkVersion: "10.0.100");

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

        var read = () => GlobalJsonModifier.GetGlobalJsonInfo(_testDir);
        var readSdk = () => GlobalJsonChannelResolver.ReadSdkSection(path);
        var update = () => GlobalJsonModifier.UpdateGlobalJson(path, "10.0.105");
        var updateIfNewer = () => GlobalJsonModifier.UpdateGlobalJsonIfNewer(path, new ReleaseVersion("10.0.105"));

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

        var read = () => GlobalJsonModifier.GetGlobalJsonInfo(_testDir);
        var readSdk = () => GlobalJsonChannelResolver.ReadSdkSection(path);
        var update = () => GlobalJsonModifier.UpdateGlobalJson(path, "10.0.105");
        var updateIfNewer = () => GlobalJsonModifier.UpdateGlobalJsonIfNewer(path, new ReleaseVersion("10.0.105"));
        var replace = () => GlobalJsonModifier.ReplaceGlobalJsonSdkVersion(contents, "10.0.105");

        read.Should().Throw<DotnetInstallException>()
            .Which.ErrorCode.Should().Be(DotnetInstallErrorCode.ContextResolutionFailed);
        readSdk.Should().Throw<JsonException>();
        update.Should().Throw<JsonException>();
        updateIfNewer.Should().Throw<JsonException>();
        replace.Should().Throw<JsonException>();
        File.ReadAllText(path).Should().Be(contents);
    }

    [TestMethod]
    [DataRow("utf-16")]
    [DataRow("utf-16BE")]
    public void GlobalJsonFileHelper_OpenAsUtf8Stream_TranscodesUtf16(string encodingName)
    {
        var encoding = Encoding.GetEncoding(encodingName);
        var json = """{ "sdk": { "version": "8.0.100" } }""";
        var path = Path.Combine(_testDir, "global.json");
        File.WriteAllText(path, json, encoding);

        using var stream = GlobalJsonFileHelper.OpenAsUtf8Stream(path);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = reader.ReadToEnd();
        content.Should().Contain("\"8.0.100\"");
    }
}
