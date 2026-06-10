// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.Tools.Bootstrapper;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class DotnetupUILanguageTests
{
    [Theory]
    [InlineData("fr_FR.UTF-8", "fr-FR")]
    [InlineData("fr_FR.UTF-8@euro", "fr-FR")]
    [InlineData("de_DE@euro", "de-DE")]
    [InlineData("pt_BR.UTF-8", "pt-BR")]
    [InlineData("ja_JP.eucJP", "ja-JP")]
    [InlineData("en_US", "en-US")]
    [InlineData("zh_CN", "zh-CN")]
    public void NormalizePosixLocale_StripsEncodingAndModifier(string posix, string expected)
    {
        DotnetupUILanguage.NormalizePosixLocale(posix).Should().Be(expected);
    }

    [Theory]
    [InlineData("C")]
    [InlineData("c")]
    [InlineData("POSIX")]
    [InlineData("posix")]
    [InlineData("")]
    [InlineData(".UTF-8")]
    public void NormalizePosixLocale_ReturnsNullForInvariantLocales(string posix)
    {
        DotnetupUILanguage.NormalizePosixLocale(posix).Should().BeNull();
    }
}

[Collection("DotnetupEnvironmentMutationTests")]
public class DotnetupUILanguageOverrideTests : IDisposable
{
    private const string DotnetCliUiLanguage = "DOTNET_CLI_UI_LANGUAGE";
    private const string VsLang = "VSLANG";

    private readonly string? _originalDotnetCliUiLanguage;
    private readonly string? _originalVsLang;

    public DotnetupUILanguageOverrideTests()
    {
        _originalDotnetCliUiLanguage = Environment.GetEnvironmentVariable(DotnetCliUiLanguage);
        _originalVsLang = Environment.GetEnvironmentVariable(VsLang);

        Environment.SetEnvironmentVariable(DotnetCliUiLanguage, null);
        Environment.SetEnvironmentVariable(VsLang, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(DotnetCliUiLanguage, _originalDotnetCliUiLanguage);
        Environment.SetEnvironmentVariable(VsLang, _originalVsLang);
    }

    [Fact]
    public void ResolveUICulture_HonorsDotnetCliUiLanguage()
    {
        Environment.SetEnvironmentVariable(DotnetCliUiLanguage, "fr-FR");

        CultureInfo? uiCulture = DotnetupUILanguage.ResolveUICulture();

        uiCulture!.Name.Should().Be("fr-FR");
    }

    [Fact]
    public void ResolveUICulture_WithoutOverride_DefersToRuntimeOffLinux()
    {
        // On Windows and macOS dotnetup runs non-invariant, so the runtime already initialized the
        // UI culture; ResolveUICulture returns null to leave it untouched. (On Linux it detects
        // from the environment, which is machine-dependent and not asserted here.)
        if (OperatingSystem.IsLinux())
        {
            return;
        }

        DotnetupUILanguage.ResolveUICulture().Should().BeNull();
    }
}
