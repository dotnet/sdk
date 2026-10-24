// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.PlatformCompatibilityAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.InteropServices.UnitTests
{
    // Reduced repros for CA1416 issues reported against the SDK and the analyzers repo.
    //
    // Tests marked '[Ignore]' are known failures: the repro is genuine and the expected diagnostics
    // written here are what the analyzer *should* produce, but fixing them is out of scope for the
    // change this file ships with. Each one names the issue it tracks.
    //
    // Not every reported issue is a bug. https://github.com/dotnet/roslyn-analyzers/issues/7392 asks
    // for the target framework's platform version to act as a floor, so that a bare
    // 'OperatingSystem.IsWindows()' would satisfy '[SupportedOSPlatform("windows5.0")]'. That is a
    // feature request rather than a defect: without a platform specific target framework the analyzer
    // has no minimum version to reason with, and 'IsWindows()' really does only establish
    // "windows, all versions". 'PlatformFloorSatisfiesVersionedRequirement' below pins the behaviour
    // that issue is really asking for and shows it already works once a floor exists.
    public partial class PlatformCompatabilityAnalyzerTests
    {
        // https://github.com/dotnet/roslyn-analyzers/issues/7661
        // The else branch of a guard for 'macos12.0' is unreachable when the call site is an allow list
        // that starts at macOS 12.0, so an API that is unsupported on macOS may be used there.
        // Fixed by 'IsPlatformExcludedByCallsite'; this test fails without that change.
        [TestMethod, WorkItem(7661, "https://github.com/dotnet/roslyn-analyzers/issues/7661")]
        public async Task NegatedGuardBranchAllowsUnsupportedApiWhenCallsiteExcludesPlatform()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                [assembly: SupportedOSPlatform("macos12.0")]

                partial class TestType
                {
                    [SupportedOSPlatform("macos12.0")]
                    public virtual object DictionaryRepresentation
                    {
                        get
                        {
                            if (IsAtLeastXcode12)
                            {
                                return _DictionaryRepresentation14;
                            }
                            else
                            {
                                return _DictionaryRepresentation13;
                            }
                        }
                    }

                    [SupportedOSPlatformGuard("macos")]
                    public ulong? _DictionaryRepresentation14 { get; private set; }

                    [UnsupportedOSPlatform("macos")]
                    public ulong? _DictionaryRepresentation13 { get; private set; }

                    [SupportedOSPlatformGuard("macos12.0")]
                    internal static bool IsAtLeastXcode12 => true;
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        // https://github.com/dotnet/roslyn-analyzers/issues/7392 is not a bug: once the assembly has a
        // platform floor, a lower versioned requirement is satisfied without any guard at all. What the
        // issue asks for is that a bare 'OperatingSystem.IsWindows()' imply the target framework's floor,
        // which the analyzer cannot do for a framework that names no platform.
        [TestMethod, WorkItem(7392, "https://github.com/dotnet/roslyn-analyzers/issues/7392")]
        public async Task PlatformFloorSatisfiesVersionedRequirement()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                [assembly: SupportedOSPlatform("windows10.0.18362.0")]

                class TestType
                {
                    [SupportedOSPlatform("windows5.0")]
                    static void Api() { }

                    static void Caller() { Api(); }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        // https://github.com/dotnet/roslyn-analyzers/issues/7578
        // A disjunct naming a platform the call site does not support widens the call site to include
        // that platform. Here the call site is an iOS only allow list, so 'IsMacOSVersionAtLeast(15)'
        // can never be true and the branch implies iOS 18.0, yet macOS 15.0 is added to the call site
        // and the iOS-18.0-only API is reported as unsupported on it.
        [TestMethod, WorkItem(7578, "https://github.com/dotnet/roslyn-analyzers/issues/7578"),
            Ignore("Known failure, see https://github.com/dotnet/roslyn-analyzers/issues/7578")]
        public async Task ImpossibleDisjunctMustNotWidenAllowListCallsite()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [SupportedOSPlatform("ios18.0")]
                    public static object Api() => null;

                    [SupportedOSPlatform("ios13.0")]
                    static void Caller()
                    {
                        if (OperatingSystem.IsIOSVersionAtLeast(18) || OperatingSystem.IsMacOSVersionAtLeast(15))
                        {
                            Api();
                        }
                    }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        // The companion of the test above, and the reason it is stated with an iOS only call site: when
        // the call site really can run on macOS the warning is correct, because the API is not supported
        // there at all. This one passes today and must keep passing after 7578 is fixed.
        [TestMethod, WorkItem(7578, "https://github.com/dotnet/roslyn-analyzers/issues/7578")]
        public async Task PossibleDisjunctOnUnsupportedPlatformIsReported()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                class TestType
                {
                    [SupportedOSPlatform("ios18.0")]
                    public static object Api() => null;

                    [SupportedOSPlatform("ios13.0")]
                    [SupportedOSPlatform("macos15.0")]
                    static void Caller()
                    {
                        if (OperatingSystem.IsIOSVersionAtLeast(18) || OperatingSystem.IsMacOSVersionAtLeast(15))
                        {
                            {|#0:Api()|};
                        }
                    }
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(0)
                    .WithArguments("TestType.Api()", "'ios' 18.0 and later, 'maccatalyst' 18.0 and later",
                        "'ios' 13.0 and later, 'maccatalyst' 13.0 and later, 'macOS/OSX' 15.0 and later"));
        }

        // https://github.com/dotnet/sdk/issues/54066
        // Putting a '[SupportedOSPlatformGuard]' on a member makes that member's call site "reachable on
        // all platforms", discarding the assembly level allow list. The guarded platform is irrelevant:
        // a guard for an entirely unrelated platform breaks it in exactly the same way, which is what
        // rules out any reading where the guard is meant to widen the call site on purpose.
        [TestMethod, WorkItem(54066, "https://github.com/dotnet/sdk/issues/54066"),
            Ignore("Known failure, see https://github.com/dotnet/sdk/issues/54066")]
        public async Task GuardAttributeOnMemberMustNotDropAssemblyAllowList()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                [assembly: SupportedOSPlatform("windows10.0.18362.0")]

                class C
                {
                    [SupportedOSPlatform("windows10.0.10240.0")]
                    static bool Api() => true;

                    static bool Unguarded() => Api();

                    [SupportedOSPlatformGuard("windows10.0.20348.0")]
                    static bool GuardedVersioned() => Api();

                    [SupportedOSPlatformGuard("windows")]
                    static bool GuardedUnversioned() => Api();

                    [SupportedOSPlatformGuard("linux")]
                    static bool GuardedOtherPlatform() => Api();
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        // https://github.com/dotnet/sdk/issues/51144
        // A false negative: when the containing type carries a 'SupportedOSPlatform' for the same
        // platform, a member that is both supported and unsupported loses its unsupported half. 'E1.A'
        // and 'E1.B' are silent while the identical members of 'E2', whose type has no attribute, are
        // correctly reported. Only 'E1.C', which has no member level 'SupportedOSPlatform', survives.
        [TestMethod, WorkItem(51144, "https://github.com/dotnet/sdk/issues/51144"),
            Ignore("Known failure, see https://github.com/dotnet/sdk/issues/51144")]
        public async Task MemberSupportedAndUnsupportedUnderSupportedTypeMustStillWarn()
        {
            var source = """
                using System;
                using System.Runtime.Versioning;

                public class C
                {
                    [SupportedOSPlatform("macos12.0")]
                    public void M()
                    {
                        Console.WriteLine({|#0:E1.A|});
                        Console.WriteLine({|#1:E1.B|});
                        Console.WriteLine({|#2:E1.C|});

                        Console.WriteLine({|#3:E2.A|});
                        Console.WriteLine({|#4:E2.B|});
                        Console.WriteLine({|#5:E2.C|});
                    }
                }

                [SupportedOSPlatform("macos")]
                public enum E1 : ulong
                {
                    [SupportedOSPlatform("macos")]
                    [UnsupportedOSPlatform("macos11.0")]
                    A,

                    [SupportedOSPlatform("macos10.0")]
                    [UnsupportedOSPlatform("macos11.0")]
                    B,

                    [UnsupportedOSPlatform("macos11.0")]
                    C,

                    Z,
                }

                public enum E2 : ulong
                {
                    [SupportedOSPlatform("macos")]
                    [UnsupportedOSPlatform("macos11.0")]
                    A,

                    [SupportedOSPlatform("macos10.0")]
                    [UnsupportedOSPlatform("macos11.0")]
                    B,

                    [UnsupportedOSPlatform("macos11.0")]
                    C,

                    Z,
                }
                """;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                Unsupported(0, "E1.A"), Unsupported(1, "E1.B"), Unsupported(2, "E1.C"),
                Unsupported(3, "E2.A"), Unsupported(4, "E2.B"), Unsupported(5, "E2.C"));

            static Microsoft.CodeAnalysis.Testing.DiagnosticResult Unsupported(int location, string member)
                => VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(location)
                    .WithArguments(member, "'macOS/OSX' 11.0 and later", "'macOS/OSX' 12.0 and later");
        }
    }
}
