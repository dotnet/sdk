// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

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

    [Fact]
    public void ReplaceGlobalJsonSdkVersion_ReplacesVersionInSdkSection()
    {
        var json = """
            {
              "sdk": {
                "version": "8.0.100"
              }
            }
            """;

        var result = DotnetInstallManager.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

        result.Should().NotBeNull();
        result.Should().Contain("\"10.0.100\"");
        result.Should().NotContain("\"8.0.100\"");
    }

    [Fact]
    public void ReplaceGlobalJsonSdkVersion_PreservesFormatting()
    {
        // Intentionally irregular whitespace
        var json = "{\n  \"sdk\" :  { \"version\" : \"8.0.100\" ,  \"rollForward\": \"latestFeature\" }\n}";

        var result = DotnetInstallManager.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

        result.Should().NotBeNull();
        // Everything except the version value should remain unchanged
        result.Should().Contain("\"rollForward\": \"latestFeature\"");
        result.Should().Contain("\"10.0.100\"");
    }

    [Fact]
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

        var result = DotnetInstallManager.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

        result.Should().NotBeNull();
        result.Should().Contain("\"10.0.100\"");
        result.Should().Contain("\"rollForward\": \"latestFeature\"");
        result.Should().Contain("\"allowPrerelease\": false");
        // The "dotnet" value in tools should NOT be changed
        result.Should().Contain("\"dotnet\": \"8.0.100\"");
    }

    [Fact]
    public void ReplaceGlobalJsonSdkVersion_ReturnsNull_WhenNoSdkSection()
    {
        var json = """
            {
              "tools": {
                "version": "8.0.100"
              }
            }
            """;

        var result = DotnetInstallManager.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

        result.Should().BeNull();
    }

    [Fact]
    public void ReplaceGlobalJsonSdkVersion_ReturnsNull_WhenNoVersionInSdk()
    {
        var json = """
            {
              "sdk": {
                "rollForward": "latestFeature"
              }
            }
            """;

        var result = DotnetInstallManager.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

        result.Should().BeNull();
    }

    [Fact]
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

        var result = DotnetInstallManager.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

        result.Should().BeNull();
    }

    [Fact]
    public void ReplaceGlobalJsonSdkVersion_ReplacesLongerVersion()
    {
        var json = """
            {
              "sdk": {
                "version": "8.0.100"
              }
            }
            """;

        var result = DotnetInstallManager.ReplaceGlobalJsonSdkVersion(json, "10.0.100-preview.1");

        result.Should().NotBeNull();
        result.Should().Contain("\"10.0.100-preview.1\"");
    }

    [Fact]
    public void ReplaceGlobalJsonSdkVersion_ReplacesShorterVersion()
    {
        var json = """
            {
              "sdk": {
                "version": "10.0.100-preview.1.12345"
              }
            }
            """;

        var result = DotnetInstallManager.ReplaceGlobalJsonSdkVersion(json, "10.0.100");

        result.Should().NotBeNull();
        result.Should().Contain("\"10.0.100\"");
        result.Should().NotContain("preview");
    }

    // ──────────────────────────────────────────────────────────────
    //  UpdateGlobalJson (file I/O layer)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateGlobalJson_DoesNothing_WhenSdkVersionIsNull()
    {
        var path = Path.Combine(_testDir, "global.json");
        var original = """{ "sdk": { "version": "8.0.100" } }""";
        File.WriteAllText(path, original);

        var manager = new DotnetInstallManager();
        manager.UpdateGlobalJson(path, sdkVersion: null);

        File.ReadAllText(path).Should().Be(original);
    }

    [Fact]
    public void UpdateGlobalJson_DoesNothing_WhenFileDoesNotExist()
    {
        var path = Path.Combine(_testDir, "nonexistent", "global.json");

        var manager = new DotnetInstallManager();

        // Should not throw
        var ex = Record.Exception(() => manager.UpdateGlobalJson(path, sdkVersion: "10.0.100"));
        ex.Should().BeNull();
    }

    [Fact]
    public void UpdateGlobalJson_UpdatesVersionInFile()
    {
        var path = Path.Combine(_testDir, "global.json");
        File.WriteAllText(path, """{ "sdk": { "version": "8.0.100" } }""");

        var manager = new DotnetInstallManager();
        manager.UpdateGlobalJson(path, sdkVersion: "10.0.100");

        var updated = File.ReadAllText(path);
        updated.Should().Contain("\"10.0.100\"");
        updated.Should().NotContain("\"8.0.100\"");
    }

    [Fact]
    public void UpdateGlobalJson_DoesNotWriteFile_WhenNoSdkVersionToReplace()
    {
        var path = Path.Combine(_testDir, "global.json");
        var original = """{ "tools": { "dotnet": "8.0.100" } }""";
        File.WriteAllText(path, original);
        var lastWrite = File.GetLastWriteTimeUtc(path);

        // Small delay so any write would be detectable
        Thread.Sleep(50);

        var manager = new DotnetInstallManager();
        manager.UpdateGlobalJson(path, sdkVersion: "10.0.100");

        File.ReadAllText(path).Should().Be(original);
        File.GetLastWriteTimeUtc(path).Should().Be(lastWrite);
    }

    // ──────────────────────────────────────────────────────────────
    //  Default behavior: --update-global-json is NOT on by default
    // ──────────────────────────────────────────────────────────────

    [Fact]
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

    [Fact]
    public void UpdateGlobalJsonOption_TrueWhenExplicitlyPassed()
    {
        var parseResult = Parser.Parse(["sdk", "install", "10.0", "--update-global-json"]);
        parseResult.Errors.Should().BeEmpty();

        var value = parseResult.GetValue(
            SdkInstallCommandParser.UpdateGlobalJsonOption);

        value.Should().Be(true);
    }

    // ──────────────────────────────────────────────────────────────
    //  UTF-16 BOM support
    // ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]    // UTF-16 LE with BOM
    [InlineData("utf-16BE")]  // UTF-16 BE with BOM
    public void GetGlobalJsonInfo_ReadsCorrectVersion_WithEncoding(string encodingName)
    {
        var encoding = Encoding.GetEncoding(encodingName);
        var json = """
            {
              "sdk": {
                "version": "9.0.200"
              }
            }
            """;
        var path = Path.Combine(_testDir, "global.json");
        File.WriteAllText(path, json, encoding);

        var manager = new DotnetInstallManager();
        var info = manager.GetGlobalJsonInfo(_testDir);

        info.GlobalJsonContents.Should().NotBeNull();
        info.GlobalJsonContents!.Sdk.Should().NotBeNull();
        info.GlobalJsonContents.Sdk!.Version.Should().Be("9.0.200");
    }

    [Theory]
    [InlineData("utf-16")]    // UTF-16 LE with BOM
    [InlineData("utf-16BE")]  // UTF-16 BE with BOM
    public void UpdateGlobalJson_PreservesEncoding_WithUtf16Bom(string encodingName)
    {
        var encoding = Encoding.GetEncoding(encodingName);
        var json = """
            {
              "sdk": {
                "version": "8.0.100"
              }
            }
            """;
        var path = Path.Combine(_testDir, "global.json");
        File.WriteAllText(path, json, encoding);

        var manager = new DotnetInstallManager();
        manager.UpdateGlobalJson(path, sdkVersion: "10.0.100");

        // Re-read with BOM detection to verify encoding was preserved
        var (content, detectedEncoding) = GlobalJsonFileHelper.ReadFileWithEncodingDetection(path);
        content.Should().Contain("\"10.0.100\"");
        content.Should().NotContain("\"8.0.100\"");
        detectedEncoding.CodePage.Should().Be(encoding.CodePage,
            $"file should still be encoded as {encodingName}");
    }

    [Theory]
    [InlineData("utf-16")]
    [InlineData("utf-16BE")]
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
