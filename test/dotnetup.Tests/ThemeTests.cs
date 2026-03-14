// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class ThemeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _originalDataDir;

    public ThemeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-theme-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _originalDataDir = Environment.GetEnvironmentVariable("DOTNET_DOTNETUP_DATA_DIR");
        Environment.SetEnvironmentVariable("DOTNET_DOTNETUP_DATA_DIR", _tempDir);

        // Start each test with a fresh theme
        DotnetupTheme.Reload();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DOTNET_DOTNETUP_DATA_DIR", _originalDataDir);
        DotnetupTheme.Reload();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
    }

    [Fact]
    public void DefaultTheme_HasExpectedValues()
    {
        var theme = DotnetupTheme.Current;

        theme.Success.Should().Be("green");
        theme.Error.Should().Be("red");
        theme.Warning.Should().Be("yellow");
        theme.Accent.Should().Be("#9780E5");
        theme.Brand.Should().Be("#9780E5");
        theme.Dim.Should().Be("dim");
    }

    [Fact]
    public void ThemeColors_RoundTrips_ThroughConfig()
    {
        var config = new DotnetupConfigData
        {
            Theme = new ThemeColors
            {
                Success = "blue",
                Error = "#FF0000",
            }
        };

        DotnetupConfig.Write(config);

        var loaded = DotnetupConfig.Read();
        loaded.Should().NotBeNull();
        loaded!.Theme.Should().NotBeNull();
        loaded.Theme!.Success.Should().Be("blue");
        loaded.Theme!.Error.Should().Be("#FF0000");
        // Unset values should keep defaults
        loaded.Theme!.Warning.Should().Be("yellow");
    }

    [Fact]
    public void Reload_PicksUpConfigChanges()
    {
        DotnetupTheme.Current.Success.Should().Be("green");

        var config = new DotnetupConfigData
        {
            Theme = new ThemeColors { Success = "cyan" }
        };
        DotnetupConfig.Write(config);
        DotnetupTheme.Reload();

        DotnetupTheme.Current.Success.Should().Be("cyan");
    }

    [Fact]
    public void MarkupHelpers_WrapTextCorrectly()
    {
        DotnetupTheme.Success("ok").Should().Be("[green]ok[/]");
        DotnetupTheme.Error("fail").Should().Be("[red]fail[/]");
        DotnetupTheme.Warning("warn").Should().Be("[yellow]warn[/]");
        DotnetupTheme.Accent("v10").Should().Be("[#9780E5]v10[/]");
        DotnetupTheme.Brand("dotnet").Should().Be("[#9780E5]dotnet[/]");
        DotnetupTheme.Dim("note").Should().Be("[dim]note[/]");
    }

    [Theory]
    [InlineData("green", true)]
    [InlineData("red", true)]
    [InlineData("mediumpurple1", true)]
    [InlineData("#9780E5", true)]
    [InlineData("#FF0000", true)]
    [InlineData("rgb(151,128,229)", true)]
    [InlineData("dim", true)]
    [InlineData("bold", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("#GGG", false)]
    [InlineData("#12345", false)]
    [InlineData("not a color!", false)]
    public void IsValidColor_ValidatesCorrectly(string color, bool expected)
    {
        DotnetupTheme.IsValidColor(color).Should().Be(expected);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("error")]
    [InlineData("warning")]
    [InlineData("accent")]
    [InlineData("brand")]
    [InlineData("dim")]
    public void Properties_ContainsAllSemanticNames(string name)
    {
        ThemeColors.s_properties.Should().ContainKey(name);
    }

    [Fact]
    public void Properties_GetAndSet_Work()
    {
        var theme = new ThemeColors();
        ThemeColors.s_properties["success"].Set(theme, "cyan");
        ThemeColors.s_properties["success"].Get(theme).Should().Be("cyan");
    }

    [Fact]
    public void Parser_ThemeShow_NoErrors()
    {
        var result = Parser.Parse(["theme"]);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_ThemeSet_NoErrors()
    {
        var result = Parser.Parse(["theme", "set", "success", "#00FF00"]);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_ThemeReset_NoErrors()
    {
        var result = Parser.Parse(["theme", "reset"]);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_ThemeUse_NoErrors()
    {
        var result = Parser.Parse(["theme", "use", "monokai"]);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_ThemeList_NoErrors()
    {
        var result = Parser.Parse(["theme", "list"]);
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("default")]
    [InlineData("standard")]
    [InlineData("monokai")]
    public void Presets_ContainsExpectedThemes(string name)
    {
        ThemeColors.s_presets.Should().ContainKey(name);
    }

    [Fact]
    public void StandardPreset_UsesPlainColors()
    {
        var standard = ThemeColors.s_presets["standard"];
        standard.Success.Should().Be("green");
        standard.Error.Should().Be("red");
        standard.Warning.Should().Be("yellow");
        standard.Accent.Should().Be("blue");
        standard.Brand.Should().Be("blue");
    }

    [Fact]
    public void MonokaiPreset_UsesMonokaiColors()
    {
        var monokai = ThemeColors.s_presets["monokai"];
        monokai.Success.Should().Be("#A6E22E");
        monokai.Error.Should().Be("#F92672");
        monokai.Warning.Should().Be("#FD971F");
        monokai.Accent.Should().Be("#66D9EF");
        monokai.Brand.Should().Be("#AE81FF");
        monokai.Dim.Should().Be("#75715E");
    }

    [Fact]
    public void UsePreset_AppliesTheme()
    {
        // Apply monokai preset via config
        DotnetupConfigData config = DotnetupConfig.Read() ?? new DotnetupConfigData();
        var preset = ThemeColors.s_presets["monokai"];
        config.Theme = new ThemeColors
        {
            Success = preset.Success,
            Error = preset.Error,
            Warning = preset.Warning,
            Accent = preset.Accent,
            Brand = preset.Brand,
            Dim = preset.Dim,
        };
        DotnetupConfig.Write(config);
        DotnetupTheme.Reload();

        DotnetupTheme.Current.Success.Should().Be("#A6E22E");
        DotnetupTheme.Current.Brand.Should().Be("#AE81FF");
    }
}
