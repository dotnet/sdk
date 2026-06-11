// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.IO;
using System.Linq;
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

    // Representative locale -> shipped satellite culture. The neutral cases (e.g. fr-FR -> fr)
    // and the Chinese script cases (zh-CN -> zh-Hans) exercise the fallback that invariant mode
    // would otherwise break. SupportedLanguageCases_CoverEverySupportedLanguage asserts there is
    // a case here for every language we localize for.
    public static IEnumerable<object[]> SupportedLanguageCases() =>
    [
        ["cs-CZ", "cs"],
        ["de-DE", "de"],
        ["es-ES", "es"],
        ["fr-FR", "fr"],
        ["it-IT", "it"],
        ["ja-JP", "ja"],
        ["ko-KR", "ko"],
        ["pl-PL", "pl"],
        ["pt-BR", "pt-BR"],
        ["ru-RU", "ru"],
        ["tr-TR", "tr"],
        ["zh-CN", "zh-Hans"],
        ["zh-TW", "zh-Hant"],
        // Additional fallback / script cases.
        ["fr", "fr"],
        ["zh-SG", "zh-Hans"],
        ["zh-HK", "zh-Hant"],
        ["zh-Hant", "zh-Hant"],
    ];

    [Theory]
    [MemberData(nameof(SupportedLanguageCases))]
    public void MatchSupportedLanguage_MapsToShippedSatellite(string input, string expected)
    {
        DotnetupUILanguage.MatchSupportedLanguage(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("en")]
    [InlineData("nl-NL")]
    [InlineData("pt-PT")]
    [InlineData("pt")]
    [InlineData("")]
    [InlineData(null)]
    public void MatchSupportedLanguage_ReturnsNullForUnsupported(string? input)
    {
        DotnetupUILanguage.MatchSupportedLanguage(input).Should().BeNull();
    }

    [Fact]
    public void SupportedLanguageCases_CoverEverySupportedLanguage()
    {
        IEnumerable<string> covered = SupportedLanguageCases().Select(c => (string)c[1]).Distinct();

        covered.Should().BeEquivalentTo(
            DotnetupUILanguage.SupportedUILanguages,
            "every shipped UI language needs a representative MatchSupportedLanguage test case");
    }

    [Fact]
    public void SupportedUILanguages_MatchesShippedSatelliteResources()
    {
        // Discover the satellite resource cultures actually emitted next to the test, so adding or
        // removing a Strings.*.xlf without updating SupportedUILanguages (and its mapping rules)
        // fails this test.
        IEnumerable<string> shipped = Directory.EnumerateDirectories(AppContext.BaseDirectory)
            .Where(directory => File.Exists(Path.Combine(directory, "dotnetup.Library.resources.dll")))
            .Select(directory => Path.GetFileName(directory));

        shipped.Should().BeEquivalentTo(
            DotnetupUILanguage.SupportedUILanguages,
            "DotnetupUILanguage.SupportedUILanguages must stay in sync with the shipped Strings.*.xlf satellites");
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
    public void ResolveUICulture_AppliesOverrideAsIsOnNonInvariantPlatforms()
    {
        // On Windows/macOS dotnetup runs non-invariant and the override is applied as-is (the
        // runtime's parent fallback resolves resources). On invariant platforms it is instead
        // mapped onto a shipped satellite, which the next test covers.
        if (DotnetupUILanguage.PlatformRunsInvariant)
        {
            return;
        }

        Environment.SetEnvironmentVariable(DotnetCliUiLanguage, "fr-FR");

        DotnetupUILanguage.ResolveUICulture()!.Name.Should().Be("fr-FR");
    }

    [Fact]
    public void ResolveUICulture_MapsOverrideToSatelliteOnInvariantPlatforms()
    {
        // On invariant platforms (Linux, etc.) the override is resolved with the same code as the
        // rest of the .NET CLI and then mapped onto a shipped satellite (fr-FR -> fr).
        if (!DotnetupUILanguage.PlatformRunsInvariant)
        {
            return;
        }

        Environment.SetEnvironmentVariable(DotnetCliUiLanguage, "fr-FR");

        DotnetupUILanguage.ResolveUICulture()!.Name.Should().Be("fr");
    }

    [Fact]
    public void ResolveUICulture_WithoutOverride_DefersToRuntimeOnNonInvariantPlatforms()
    {
        // On Windows and macOS the runtime already initialized CurrentUICulture from the OS, so
        // ResolveUICulture returns null to leave it untouched.
        if (DotnetupUILanguage.PlatformRunsInvariant)
        {
            return;
        }

        DotnetupUILanguage.ResolveUICulture().Should().BeNull();
    }
}
