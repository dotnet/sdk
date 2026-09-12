// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.PlatformCompatibilityAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.InteropServices.UnitTests
{
    // Systematic coverage of how the platform-availability attributes combine. The attribute model has a
    // handful of independent dimensions, and most of the interesting behavior lives where they meet:
    //
    //   attribute kind    - SupportedOSPlatform / UnsupportedOSPlatform / ObsoletedOSPlatform
    //   version           - absent (treated as 0.0), present, or equal to another attribute's version
    //   multiplicity      - one, two, or three attributes of the same kind on the same platform
    //   platform relation - unrelated platforms (windows, macos, tvos, ...) versus the single related pair
    //                       ios -> maccatalyst, where an ios attribute is also inferred for maccatalyst
    //   declaration level - assembly, type, or member
    //   call site shape   - reachable everywhere, allow list, or deny list
    //   spelling          - 'OSX' is an alias of 'macos'
    //
    // Two rules carry most of the weight and are asserted repeatedly below:
    //
    //   1. Unrelated platforms must never borrow each other's *versions*. The version range an attribute
    //      establishes for macOS must not end up applied to tvOS. Violations of this rule are what
    //      https://github.com/dotnet/roslyn-analyzers/issues/7665 was about.
    //      This is not the same thing as saying an attribute for macOS has no effect on tvOS. It does:
    //      a single 'SupportedOSPlatform' turns the API into an allow list, so every platform without its
    //      own 'SupportedOSPlatform' becomes unsupported. That is the intended, platform agnostic
    //      behaviour of allow lists, and it is orthogonal to the per platform version ranges asserted
    //      here; the tests below always give each platform under test its own attribute so that the
    //      allow list effect does not mask a leaked version.
    //   2. maccatalyst is the one exception: 'TryAddValidAttribute' mirrors every ios attribute onto
    //      maccatalyst, because code compiled for Mac Catalyst runs the iOS surface. The mirroring has to
    //      hold for *every* attribute kind and for the cancelling case, not just the simple ones.
    public partial class PlatformCompatabilityAnalyzerTests
    {
        // Same as 's_msBuildPlatforms' but with tvos, so that deny lists on tvos are considered.
        private const string s_platformsWithTvOs = "build_property._SupportedPlatformList=windows,browser,macOS,maccatalyst, ios, linux, tvos;\nbuild_property.TargetFramework=net5.0\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v5.0";

        #region Supported/Unsupported naming the same version

        // 'AddAttribute' treats a Supported and an Unsupported attribute naming the *same* version as
        // cancelling each other out, and reports that by returning false so the platform is dropped
        // entirely. For ios that has to drop the mirrored maccatalyst entry too: the only reason
        // maccatalyst had an unsupported 12.0 at all was the ios attribute that just got cancelled.
        // Before this was fixed, 'ios' was cleared but 'maccatalyst' kept a phantom 'unsupported 12.0'.
        [TestMethod]
        public async Task SameVersionUnsupportedThenSupportedCancelsOutOnIosAndMacCatalyst()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [UnsupportedOSPlatform("ios12.0")]
                    [SupportedOSPlatform("ios12.0")]
                    public static void Api() { }

                    static void CrossPlatformCaller() { Api(); }

                    [SupportedOSPlatform("ios")]
                    static void IosCaller() { Api(); }

                    [SupportedOSPlatform("maccatalyst")]
                    static void MacCatalystCaller() { Api(); }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        // The control for the test above: on a platform with no related platform the pair has always
        // cancelled out cleanly, so ios/maccatalyst must end up in the same state.
        [TestMethod]
        public async Task SameVersionUnsupportedThenSupportedCancelsOutOnMacCatalyst()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [UnsupportedOSPlatform("maccatalyst12.0")]
                    [SupportedOSPlatform("maccatalyst12.0")]
                    public static void Api() { }

                    static void CrossPlatformCaller() { Api(); }

                    [SupportedOSPlatform("maccatalyst")]
                    static void MacCatalystCaller() { Api(); }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        // Source order matters here, and this documents the other order rather than endorsing it.
        // With Supported first, 'AddAttribute' clears SupportedFirst and then records the Unsupported,
        // so the platform survives as a deny list instead of disappearing. The mirroring onto
        // maccatalyst is what matters for this file: whatever ios ends up as, maccatalyst must match.
        [TestMethod]
        public async Task SameVersionSupportedThenUnsupportedIsDenyListOnIosAndMacCatalyst()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [SupportedOSPlatform("ios12.0")]
                    [UnsupportedOSPlatform("ios12.0")]
                    public static void Api() { }

                    static void CrossPlatformCaller() { {|#0:Api()|}; }

                    [SupportedOSPlatform("ios")]
                    static void IosCaller() { {|#1:Api()|}; }

                    [SupportedOSPlatform("maccatalyst")]
                    static void MacCatalystCaller() { {|#2:Api()|}; }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(0)
                    .WithArguments("TestType.Api()", "'ios' 12.0 and later, 'maccatalyst' 12.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(1)
                    .WithArguments("TestType.Api()", "'ios' 12.0 and later, 'maccatalyst' 12.0 and later", "'ios' all versions, 'maccatalyst' all versions"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(2)
                    .WithArguments("TestType.Api()", "'maccatalyst' 12.0 and later", "'maccatalyst' all versions"));
        }

        // The same order on a platform without a related platform, to show the deny-list outcome is the
        // general behavior and not something specific to the ios/maccatalyst mirroring.
        [TestMethod]
        public async Task SameVersionSupportedThenUnsupportedIsDenyListOnMacOs()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [SupportedOSPlatform("macos12.0")]
                    [UnsupportedOSPlatform("macos12.0")]
                    public static void Api() { }

                    static void CrossPlatformCaller() { {|#0:Api()|}; }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(0)
                    .WithArguments("TestType.Api()", "'macOS/OSX' 12.0 and later"));
        }

        #endregion

        #region Every attribute kind is mirrored from ios onto maccatalyst

        [TestMethod]
        public async Task ObsoletedOnIosIsInferredForMacCatalyst()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;
                using Mock;

                class TestType
                {
                    [ObsoletedOSPlatform("ios15.0")]
                    public static void Api() { }

                    [SupportedOSPlatform("ios16.0")]
                    static void IosCaller() { {|#0:Api()|}; }

                    [SupportedOSPlatform("maccatalyst16.0")]
                    static void MacCatalystCaller() { {|#1:Api()|}; }
                }
                """ + MockObsoletedAttributeCS;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.ObsoletedCsReachable).WithLocation(0)
                    .WithArguments("TestType.Api()", "'ios' 15.0 and later, 'maccatalyst' 15.0 and later", "'ios' 16.0 and later, 'maccatalyst' 16.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.ObsoletedCsReachable).WithLocation(1)
                    .WithArguments("TestType.Api()", "'maccatalyst' 15.0 and later", "'maccatalyst' 16.0 and later"));
        }

        // The 'unsupported, then supported again, then unsupported again' shape fills both
        // UnsupportedFirst and UnsupportedSecond. The whole range has to be mirrored, not just the
        // first attribute.
        [TestMethod]
        public async Task UnsupportedVersionRangeOnIosIsInferredForMacCatalyst()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [UnsupportedOSPlatform("ios")]
                    [SupportedOSPlatform("ios13.0")]
                    [UnsupportedOSPlatform("ios16.0")]
                    public static void Api() { }

                    [SupportedOSPlatform("ios14.0")]
                    static void IosInRangeCaller() { {|#0:Api()|}; }

                    [SupportedOSPlatform("maccatalyst14.0")]
                    static void MacCatalystInRangeCaller() { {|#1:Api()|}; }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0)
                    .WithArguments("TestType.Api()", "'ios' 16.0 and later, 'maccatalyst' 16.0 and later", "'ios' 14.0 and later, 'maccatalyst' 14.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(1)
                    .WithArguments("TestType.Api()", "'maccatalyst' 16.0 and later", "'maccatalyst' 14.0 and later"));
        }

        // All three kinds at once on ios, each mirrored onto maccatalyst, and CA1416 and CA1422 reported
        // independently of one another.
        [TestMethod]
        public async Task SupportedObsoletedAndUnsupportedOnIosAreAllInferredForMacCatalyst()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;
                using Mock;

                class TestType
                {
                    [SupportedOSPlatform("ios11.0")]
                    [ObsoletedOSPlatform("ios13.0")]
                    [System.Runtime.Versioning.UnsupportedOSPlatform("ios16.0")]
                    public static void Api() { }

                    [SupportedOSPlatform("ios12.0")]
                    static void IosCaller() { {|#0:{|#1:Api()|}|}; }

                    [SupportedOSPlatform("maccatalyst12.0")]
                    static void MacCatalystCaller() { {|#2:{|#3:Api()|}|}; }
                }
                """ + MockObsoletedAttributeCS;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0)
                    .WithArguments("TestType.Api()", "'ios' 16.0 and later, 'maccatalyst' 16.0 and later", "'ios' 12.0 and later, 'maccatalyst' 12.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.ObsoletedCsReachable).WithLocation(1)
                    .WithArguments("TestType.Api()", "'ios' 13.0 and later, 'maccatalyst' 13.0 and later", "'ios' 12.0 and later, 'maccatalyst' 12.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(2)
                    .WithArguments("TestType.Api()", "'maccatalyst' 16.0 and later", "'maccatalyst' 12.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.ObsoletedCsReachable).WithLocation(3)
                    .WithArguments("TestType.Api()", "'maccatalyst' 13.0 and later", "'maccatalyst' 12.0 and later"));
        }

        [TestMethod]
        public async Task AssemblyLevelIosSupportReachesMacCatalystCallSites()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                [assembly: SupportedOSPlatform("ios14.0")]

                class TestType
                {
                    [SupportedOSPlatform("maccatalyst15.0")]
                    public static void MacCatalystApi() { }

                    [SupportedOSPlatform("ios15.0")]
                    public static void IosApi() { }

                    static void Caller()
                    {
                        {|#0:MacCatalystApi()|};
                        {|#1:IosApi()|};
                    }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(0)
                    .WithArguments("TestType.MacCatalystApi()", "'maccatalyst' 15.0 and later", "'ios' 14.0 and later, 'maccatalyst' 14.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(1)
                    .WithArguments("TestType.IosApi()", "'ios' 15.0 and later, 'maccatalyst' 15.0 and later", "'ios' 14.0 and later, 'maccatalyst' 14.0 and later"));
        }

        #endregion

        #region Custom guards and type/member merging across the ios/maccatalyst pair

        // A *custom* versioned guard property for ios guards the ios API, but it does not make a
        // maccatalyst-only API reachable. This matches the built-in 'OperatingSystem.IsIOS()' guard,
        // which deliberately does not imply Mac Catalyst reachability (see 'IosGuardsMacCatalystAsync').
        [TestMethod]
        public async Task CustomVersionedIosGuardDoesNotGuardMacCatalystOnlyApi()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [SupportedOSPlatform("ios15.0")]
                    public static void IosApi() { }

                    [SupportedOSPlatform("maccatalyst15.0")]
                    public static void MacCatalystApi() { }

                    [SupportedOSPlatformGuard("ios15.0")]
                    static bool IsIos15 => true;

                    static void Caller()
                    {
                        if (IsIos15)
                        {
                            IosApi();
                            {|#0:MacCatalystApi()|};
                        }
                    }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(0)
                    .WithArguments("TestType.MacCatalystApi()", "'maccatalyst' 15.0 and later", "'ios' 15.0 and later"));
        }

        // A member level maccatalyst attribute narrows the maccatalyst range inferred from a type level
        // ios attribute; the ios range itself is untouched, so an ios call site only warns because it is
        // also reachable on Mac Catalyst.
        [TestMethod]
        public async Task MemberLevelMacCatalystNarrowsTypeLevelIosInferredSupport()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                [SupportedOSPlatform("ios13.0")]
                class IosType
                {
                    [SupportedOSPlatform("maccatalyst15.0")]
                    public static void MacCatalystNarrowed() { }

                    public static void JustIos() { }
                }

                class Caller
                {
                    [SupportedOSPlatform("maccatalyst14.0")]
                    static void MacCatalyst14() { {|#0:IosType.MacCatalystNarrowed()|}; IosType.JustIos(); }

                    [SupportedOSPlatform("ios14.0")]
                    static void Ios14() { {|#1:IosType.MacCatalystNarrowed()|}; IosType.JustIos(); }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(0)
                    .WithArguments("IosType.MacCatalystNarrowed()", "'maccatalyst' 15.0 and later", "'maccatalyst' 14.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(1)
                    .WithArguments("IosType.MacCatalystNarrowed()", "'maccatalyst' 15.0 and later", "'ios' 14.0 and later, 'maccatalyst' 14.0 and later"));
        }

        // A member level Supported attribute cannot re-enable a platform that the type level deny list
        // turned off. This holds for the maccatalyst entry inferred from ios exactly as it does for an
        // explicitly written one, see 'MemberSupportDoesNotReEnableExplicitlyUnsupportedMacCatalyst'.
        [TestMethod]
        public async Task MemberSupportDoesNotReEnableMacCatalystUnsupportedThroughIos()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                [UnsupportedOSPlatform("ios")]
                class NoIosType
                {
                    [SupportedOSPlatform("maccatalyst14.0")]
                    public static void MacCatalystOnly() { }

                    public static void Inherited() { }
                }

                class Caller
                {
                    [SupportedOSPlatform("maccatalyst14.0")]
                    static void MacCatalyst14() { {|#0:NoIosType.MacCatalystOnly()|}; {|#1:NoIosType.Inherited()|}; }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0)
                    .WithArguments("NoIosType.MacCatalystOnly()", "'maccatalyst' all versions", "'maccatalyst' 14.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(1)
                    .WithArguments("NoIosType.Inherited()", "'maccatalyst' all versions", "'maccatalyst' 14.0 and later"));
        }

        // The control for the test above: writing the maccatalyst deny list explicitly gives the exact
        // same result, which is what makes the inferred entry correct rather than a quirk.
        [TestMethod]
        public async Task MemberSupportDoesNotReEnableExplicitlyUnsupportedMacCatalyst()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                [UnsupportedOSPlatform("maccatalyst")]
                class NoMcType
                {
                    [SupportedOSPlatform("maccatalyst14.0")]
                    public static void MacCatalystOnly() { }
                }

                class Caller
                {
                    [SupportedOSPlatform("maccatalyst14.0")]
                    static void MacCatalyst14() { {|#0:NoMcType.MacCatalystOnly()|}; }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0)
                    .WithArguments("NoMcType.MacCatalystOnly()", "'maccatalyst' all versions", "'maccatalyst' 14.0 and later"));
        }

        #endregion

        #region Explicit maccatalyst attributes versus the inferred ones

        // An explicit maccatalyst attribute does not replace the inferred one, it is merged with it, and
        // 'AddOrUpdateSupportedAttribute' keeps the *lowest* version. The explicit 'maccatalyst14.0' is
        // therefore swallowed by the 13.0 inferred from ios, and maccatalyst keeps the ios range.
        [TestMethod]
        public async Task ExplicitMacCatalystSupportDoesNotRaiseLowerInferredIosSupport()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [UnsupportedOSPlatform("ios")]
                    [SupportedOSPlatform("ios13.0")]
                    [UnsupportedOSPlatform("ios16.0")]
                    [SupportedOSPlatform("maccatalyst14.0")]
                    public static void Api() { }

                    [SupportedOSPlatform("ios14.0")]
                    static void IosCaller() { {|#0:Api()|}; }

                    [SupportedOSPlatform("maccatalyst14.0")]
                    static void MacCatalystCaller() { {|#1:Api()|}; }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0)
                    .WithArguments("TestType.Api()", "'ios' 16.0 and later, 'maccatalyst' 16.0 and later", "'ios' 14.0 and later, 'maccatalyst' 14.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(1)
                    .WithArguments("TestType.Api()", "'maccatalyst' 16.0 and later", "'maccatalyst' 14.0 and later"));
        }

        // An explicit Supported for maccatalyst does cancel the unsupport inferred from ios, so the API
        // stays usable from a Mac Catalyst call site while remaining unsupported on iOS.
        [TestMethod]
        public async Task UnsupportedIosWithExplicitMacCatalystSupportKeepsMacCatalystUsable()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [UnsupportedOSPlatform("ios12.0")]
                    [SupportedOSPlatform("maccatalyst13.0")]
                    public static void Api() { }

                    [SupportedOSPlatform("ios13.0")]
                    static void IosCaller() { {|#0:Api()|}; }

                    [SupportedOSPlatform("maccatalyst14.0")]
                    static void MacCatalystCaller() { Api(); }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0)
                    .WithArguments("TestType.Api()", "'ios' 12.0 and later", "'ios' 13.0 and later, 'maccatalyst' 13.0 and later"));
        }

        // A versioned ios guard narrows reachability to ios only - it does not make a maccatalyst-only
        // API callable. This mirrors the unversioned behavior already covered by 'IosGuardsMacCatalystAsync'.
        [TestMethod]
        public async Task VersionedIosGuardDoesNotImplyMacCatalystSupport()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [SupportedOSPlatform("maccatalyst15.0")]
                    public static void MacCatalystApi() { }

                    [SupportedOSPlatform("ios15.0")]
                    public static void IosApi() { }

                    static void Caller()
                    {
                        if (OperatingSystem.IsIOSVersionAtLeast(15))
                        {
                            {|#0:MacCatalystApi()|};
                            IosApi();
                        }

                        if (OperatingSystem.IsMacCatalystVersionAtLeast(15))
                        {
                            MacCatalystApi();
                            IosApi();
                        }
                    }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(0)
                    .WithArguments("TestType.MacCatalystApi()", "'maccatalyst' 15.0 and later", "'IOS' 15.0 and later"));
        }

        #endregion

        #region Unrelated platforms stay independent

        // The other half of rule 1 in the header: a lone macOS 'SupportedOSPlatform' *does* affect tvOS,
        // by making the API an allow list that tvOS is not on. That is intended and platform agnostic,
        // and it is why every test in this region gives each platform its own attribute.
        [TestMethod]
        public async Task LoneSupportedAttributeMakesEveryOtherPlatformUnsupported()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [SupportedOSPlatform("macos11.0")]
                    public static void Api() { }

                    [SupportedOSPlatform("tvos13.0")]
                    static void TvOsCaller() { {|#0:Api()|}; }

                    [SupportedOSPlatform("macos12.0")]
                    static void MacOsCaller() { Api(); }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_platformsWithTvOs,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(0)
                    .WithArguments("TestType.Api()", "'macOS/OSX' 11.0 and later", "'tvos' 13.0 and later"));
        }

        // ios -> maccatalyst is the only inference. macOS and tvOS attributes on the same API must be
        // evaluated separately: the macOS call site is fine, and only the tvOS one below 13.0 warns.
        [TestMethod, WorkItem(7665, "https://github.com/dotnet/roslyn-analyzers/issues/7665")]
        public async Task MacOsAndTvOsAttributesDoNotInteract()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [SupportedOSPlatform("macos11.0")]
                    [SupportedOSPlatform("tvos13.0")]
                    public static void Api() { }

                    [SupportedOSPlatform("macos12.0")]
                    static void MacOsCaller() { Api(); }

                    [SupportedOSPlatform("tvos12.0")]
                    static void TvOsBelowCaller() { {|#0:Api()|}; }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_platformsWithTvOs,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(0)
                    .WithArguments("TestType.Api()", "'macOS/OSX' 11.0 and later, 'tvos' 13.0 and later", "'tvos' 12.0 and later"));
        }

        // Four platforms on one API, where every call site satisfies its own platform's requirement.
        // Nothing is reported, which is the point: no platform borrows another's version.
        [TestMethod, WorkItem(7665, "https://github.com/dotnet/roslyn-analyzers/issues/7665")]
        public async Task UnrelatedPlatformsAreEvaluatedIndependently()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [SupportedOSPlatform("macos11.0")]
                    [SupportedOSPlatform("tvos13.0")]
                    [SupportedOSPlatform("ios14.0")]
                    public static void Api() { }

                    [SupportedOSPlatform("maccatalyst14.0")]
                    static void MacCatalystCaller() { Api(); }

                    [SupportedOSPlatform("tvos14.0")]
                    static void TvOsCaller() { Api(); }

                    [SupportedOSPlatform("macos12.0")]
                    static void MacOsCaller() { Api(); }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_platformsWithTvOs);
        }

        #endregion

        #region Version and name parsing

        // 'OSX' is normalized to 'macos', so the two spellings describe one platform: the Supported from
        // the 'OSX' attribute and the Unsupported from the 'macos' attribute form a single version range.
        [TestMethod]
        public async Task OsxAliasAndMacOsFoldIntoOnePlatform()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [SupportedOSPlatform("OSX12.0")]
                    [UnsupportedOSPlatform("macos14.0")]
                    public static void Api() { }

                    [SupportedOSPlatform("macos13.0")]
                    static void MacOs13Caller() { {|#0:Api()|}; }

                    [SupportedOSPlatform("macos15.0")]
                    static void MacOs15Caller() { {|#1:Api()|}; }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0)
                    .WithArguments("TestType.Api()", "'macOS/OSX' 14.0 and later", "'macOS/OSX' 13.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(1)
                    .WithArguments("TestType.Api()", "'macOS/OSX' 14.0 and later", "'macOS/OSX' 15.0 and later"));
        }

        // 'AddOrUpdateUnsupportedAttribute' only keeps the two lowest versions; a third, higher one has
        // nowhere to go and is dropped. Only the lowest is reported here because nothing re-supports the
        // platform in between.
        [TestMethod]
        public async Task OnlyTheTwoLowestUnsupportedVersionsAreKept()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [UnsupportedOSPlatform("macos5.0")]
                    [UnsupportedOSPlatform("macos3.0")]
                    [UnsupportedOSPlatform("macos7.0")]
                    public static void Api() { }

                    static void CrossPlatformCaller() { {|#0:Api()|}; }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(0)
                    .WithArguments("TestType.Api()", "'macOS/OSX' 3.0 and later"));
        }

        [TestMethod]
        public async Task LowestSupportedVersionWinsAndIsInferredForMacCatalyst()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [SupportedOSPlatform("ios15.0")]
                    [SupportedOSPlatform("ios13.0")]
                    public static void Api() { }

                    static void CrossPlatformCaller() { {|#0:Api()|}; }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(0)
                    .WithArguments("TestType.Api()", "'ios' 13.0 and later, 'maccatalyst' 13.0 and later"));
        }

        [TestMethod]
        public async Task LowestObsoletedVersionWinsAndIsInferredForMacCatalyst()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;
                using Mock;

                class TestType
                {
                    [ObsoletedOSPlatform("ios15.0")]
                    [ObsoletedOSPlatform("ios13.0")]
                    public static void Api() { }

                    [SupportedOSPlatform("ios12.0")]
                    static void IosCaller() { {|#0:Api()|}; }
                }
                """ + MockObsoletedAttributeCS;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.ObsoletedCsReachable).WithLocation(0)
                    .WithArguments("TestType.Api()", "'ios' 13.0 and later, 'maccatalyst' 13.0 and later", "'ios' 12.0 and later, 'maccatalyst' 12.0 and later"));
        }

        // A four part version survives parsing intact, while a platform string that starts with a digit
        // is not a valid name/version pair at all and the attribute is ignored outright - the method ends
        // up with no platform requirement rather than an empty-versioned one.
        [TestMethod]
        public async Task FourPartVersionIsParsedAndDigitLeadingPlatformNameIsIgnored()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [SupportedOSPlatform("windows10.0.19041.0")]
                    public static void FourPartVersion() { }

                    [SupportedOSPlatform("1windows")]
                    public static void NameStartsWithDigit() { }

                    static void Caller()
                    {
                        {|#0:FourPartVersion()|};
                        NameStartsWithDigit();
                    }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(0)
                    .WithArguments("TestType.FourPartVersion()", "'windows' 10.0.19041.0 and later"));
        }

        #endregion
    }
}
