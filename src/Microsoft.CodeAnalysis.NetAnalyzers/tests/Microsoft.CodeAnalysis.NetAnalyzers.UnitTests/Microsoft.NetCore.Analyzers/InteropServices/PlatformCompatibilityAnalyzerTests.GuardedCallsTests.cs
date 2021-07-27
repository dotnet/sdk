// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Test.Utilities;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.PlatformCompatibilityAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.InteropServices.UnitTests
{
    public partial class PlatformCompatabilityAnalyzerTests
    {
        public static IEnumerable<object[]> NamedArgumentsData()
        {
            yield return new object[] { "minor : 2, major : 12" };
            yield return new object[] { "build : 2, major : 12, revision : 556" };
            yield return new object[] { "build : 1, minor : 1, major : 12" };
            yield return new object[] { "revision : 555, major : 12, build : 2" };
            yield return new object[] { "major : 13, build : 3" };
        }

        [Theory]
        [MemberData(nameof(NamedArgumentsData))]
        public async Task GuardMethodWithNamedArgumentsTest(string arguments)
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    public void Api_Usage()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(" + arguments + @"))
        {
            Api();
        }
        [|Api()|];

        if (OperatingSystem.IsOSPlatformVersionAtLeast(" + arguments + @", platform : ""Android""))
        {
            Api();
        }
        else
        {
            [|Api()|];
        }
    }

    [SupportedOSPlatform(""Android12.0.2.521"")]
    void Api() { }
}";

            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task MethodsWithOsDependentTypeParameterGuarded()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;

[SupportedOSPlatform(""windows"")]
class WindowsOnlyType {}

class GenericClass<T> {}

public class Test
{
    void GenericMethod<T>() {}
    void GenericMethod2<T1, T2>() {}
    void M1()
    {
        if (OperatingSystem.IsWindows())
        {
            GenericMethod<WindowsOnlyType>();
            GenericMethod2<Test, WindowsOnlyType>();
            GenericClass<WindowsOnlyType> obj = new GenericClass<WindowsOnlyType>();
        }
        else
        {
            [|GenericMethod<WindowsOnlyType>()|];
            [|GenericMethod2<Test, WindowsOnlyType>()|];
            GenericClass<WindowsOnlyType> obj = [|new GenericClass<WindowsOnlyType>()|];
        }
    }
}
";
            await VerifyAnalyzerAsyncCs(csSource, s_msBuildPlatforms);
        }

        [Fact]
        public async Task PlatformDependentMethodsAndTypeParametersGuarded()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;

[SupportedOSPlatform(""browser"")]
class BrowserOnlyType {}
[SupportedOSPlatform(""windows"")]
class WindowsOnlyType {}

public class Test
{
    [SupportedOSPlatform(""windows"")]
    void WindowsOnlyMethod<T>() {}
    void GenericMethod<T1, T2>() {}
    void M1()
    {
        if (OperatingSystem.IsWindows())
        {
            [|WindowsOnlyMethod<BrowserOnlyType>()|];  // should flag for BrowserOnlyType parameter
            [|GenericMethod<WindowsOnlyType, BrowserOnlyType>()|]; // same
        }
        else
        {
            [|WindowsOnlyMethod<BrowserOnlyType>()|];  // should flag for WindowsOnlyMethod method and BrowserOnlyType parameter
            [|GenericMethod<WindowsOnlyType, BrowserOnlyType>()|]; // should flag for WindowsOnlyType and BrowserOnlyType parameters
        }
    }
}
";
            await VerifyAnalyzerAsyncCs(csSource, s_msBuildPlatforms,
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(24, 13).WithArguments("BrowserOnlyType", "'browser'"),
#pragma warning restore RS0030 // Do not used banned APIs
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(25, 13).WithArguments("WindowsOnlyType", "'windows'"));
#pragma warning restore RS0030 // Do not used banned APIs
        }

        [Fact]
        public async Task SupportedUnsupportedRange_GuardedWithOr()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    public void Api_Usage()
    {
        if (!OperatingSystem.IsWindows() ||
            OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            Api();
        }

        [|Api()|]; // This call site is reachable on all platforms. 'Test.Api()' is supported on: 'windows' 10.0.19041 and later.
    }

    [UnsupportedOSPlatform(""windows"")]
    [SupportedOSPlatform(""windows10.0.19041"")]
    void Api() { }
}";

            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact, WorkItem(4932, "https://github.com/dotnet/roslyn-analyzers/issues/4932")]
        public async Task GuardMethodWith3VersionPartsEquavalentTo4PartsWithLeading0()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class MSAL
{
    [SupportedOSPlatform(""windows10.0.17763.0"")]
    public static void UseWAMWithLeading0() { }

    [SupportedOSPlatform(""windows10.0.17763"")]
    public static void UseWAMNoLeading0() { }

    static void Test()
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763, 0))
        {
            UseWAMWithLeading0();
            UseWAMNoLeading0();
        }

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            UseWAMWithLeading0();
            UseWAMNoLeading0();
        }
    }
}";

            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardMethodWith1VersionPartsEquavalentTo2PartsWithLeading0()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class MSAL
{
    [SupportedOSPlatform(""windows10.0"")]
    public static void UseWAMWithLeading0() { }

    static void Test()
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            UseWAMWithLeading0();
        }

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0))
        {
            UseWAMWithLeading0();
        }
    }
}";

            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardMethodWith2VersionPartsEquavalentTo3PartsWithLeading0()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class MSAL
{
    [SupportedOSPlatform(""windows10.1.0"")]
    public static void UseWAMWithLeading0() { }

    [SupportedOSPlatform(""windows10.1"")]
    public static void UseWAMNoLeading0() { }

    static void Test()
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 1))
        {
            UseWAMWithLeading0();
            UseWAMNoLeading0();
        }

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 1, 0))
        {
            UseWAMWithLeading0();
            UseWAMNoLeading0();
        }
    }
}";

            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardsAroundSupported_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

namespace PlatformCompatDemo.Bugs.GuardsAroundSupported
{
    class Caller
    {
        public static void TestWithGuardMethods()
        {
            if (OperatingSystem.IsWindows() || OperatingSystem.IsBrowser())
            {
                [|Target.SupportedOnWindows()|]; // This call site is reachable on: 'Windows', 'Browser'. 'Target.SupportedOnWindows()' is only supported on: 'windows'.
                [|Target.SupportedOnWindows10()|];
                Target.SupportedOnWindowsAndBrowser();
                [|Target.SupportedOnWindows10AndBrowser()|]; // This call site is reachable on: 'Windows' all versions, 'Browser'. 'Target.SupportedOnWindows10AndBrowser()' is only supported on: 'windows' 10.0 and later, 'browser'.
            }
        }
    }

    class Target
    {
        [SupportedOSPlatform(""windows"")]
        public static void SupportedOnWindows() { }

        [SupportedOSPlatform(""windows10.0"")]
        public static void SupportedOnWindows10() { }

        [SupportedOSPlatform(""windows""), SupportedOSPlatform(""browser"")]
        public static void SupportedOnWindowsAndBrowser() { }

        [SupportedOSPlatform(""windows10.0""), SupportedOSPlatform(""browser"")]
        public static void SupportedOnWindows10AndBrowser() { }
    }
}";

            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardsAroundSupported()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

namespace PlatformCompatDemo.Bugs.GuardsAroundSupported
{
    class Caller
    {
        public static void TestWithGuardMethods()
        {
            if (!OperatingSystem.IsWindows())
            {
                [|Target.SupportedOnWindows()|]; // This call site is reachable on all platforms. 'Target.SupportedOnWindows()' is only supported on: 'windows'.
                [|Target.SupportedOnWindows10()|];
                [|Target.SupportedOnWindowsAndBrowser()|];   // This call site is reachable on all platforms. 'Target.SupportedOnWindowsAndBrowser()' is only supported on: 'windows', 'browser'.
                [|Target.SupportedOnWindows10AndBrowser()|]; // expected two diagnostics - supported on windows 10 and browser
            }

            if (OperatingSystem.IsWindows())
            {
                Target.SupportedOnWindows();
                [|Target.SupportedOnWindows10()|]; // This call site is reachable on: 'Windows' all versions. 'Target.SupportedOnWindows10()' is only supported on: 'windows' 10.0 and later.
                Target.SupportedOnWindowsAndBrowser();       // no diagnostic expected - the API is supported on windows, no need to warn for other platforms support
                [|Target.SupportedOnWindows10AndBrowser()|]; // This call site is reachable on: 'Windows' all versions. 'Target.SupportedOnWindows10AndBrowser()' is only supported on: 'windows' 10.0 and later, 'browser'.
            }

            if (OperatingSystem.IsWindowsVersionAtLeast(10))
            {
                Target.SupportedOnWindows();
                Target.SupportedOnWindows10();
                Target.SupportedOnWindowsAndBrowser();   // no diagnostic expected - the API is supported on windows, no need to warn for other platforms support
                Target.SupportedOnWindows10AndBrowser(); // the same, no diagnostic expected
            }

            if (OperatingSystem.IsBrowser())
            {
                [|Target.SupportedOnWindows()|]; // This call site is reachable on: 'Browser'. 'Target.SupportedOnWindows()' is only supported on: 'windows'.
                [|Target.SupportedOnWindows10()|];
                Target.SupportedOnWindowsAndBrowser();   // No diagnostic expected - the API is supported on browser, no need to warn for other platforms support
                Target.SupportedOnWindows10AndBrowser(); // The same, no diagnostic expected
            }

            if (OperatingSystem.IsWindows() || OperatingSystem.IsBrowser())
            {
                [|Target.SupportedOnWindows()|]; // This call site is reachable on: 'Windows', 'Browser'. 'Target.SupportedOnWindows()' is only supported on: 'windows'.
                [|Target.SupportedOnWindows10()|]; // This call site is reachable on: 'Windows' all versions. 'Target.SupportedOnWindows10()' is only supported on: 'windows' 10.0 and later.
                Target.SupportedOnWindowsAndBrowser();
                [|Target.SupportedOnWindows10AndBrowser()|]; // This call site is reachable on: 'Windows' all versions. 'Target.SupportedOnWindows10AndBrowser()' is only supported on: 'windows' 10.0 and later, 'browser'.
            }

           if (OperatingSystem.IsWindowsVersionAtLeast(10) || OperatingSystem.IsBrowser())
            {
                [|Target.SupportedOnWindows()|]; //  This call site is reachable on: 'Browser'. 'Target.SupportedOnWindows()' is only supported on: 'windows'.
                [|Target.SupportedOnWindows10()|]; // This call site is reachable on: 'Browser'. 'Target.SupportedOnWindows10()' is only supported on: 'windows' 10.0 and later. 
                Target.SupportedOnWindowsAndBrowser();
                Target.SupportedOnWindows10AndBrowser();
            }
        }
    }

    class Target
    {
        [SupportedOSPlatform(""windows"")]
        public static void SupportedOnWindows() { }

        [SupportedOSPlatform(""windows10.0"")]
        public static void SupportedOnWindows10() { }

        [SupportedOSPlatform(""windows""), SupportedOSPlatform(""browser"")]
        public static void SupportedOnWindowsAndBrowser() { }

        [SupportedOSPlatform(""windows10.0""), SupportedOSPlatform(""browser"")]
        public static void SupportedOnWindows10AndBrowser() { }
    }
}";

            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task MoreGuardsAroundSupported()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

namespace PlatformCompatDemo.SupportedUnupported
{
    class Caller
    {
        public static void UnsupportedCombinations()
        {
            if (OperatingSystem.IsBrowser())
            {
                var withoutAttributes = new TypeWithoutAttributes();
                withoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11();
                withoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12();
                withoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12SupportedOnWindows13();

                var unsupportedOnWindows = new TypeUnsupportedOnWindows();
                unsupportedOnWindows.FunctionSupportedOnWindows11();
                unsupportedOnWindows.FunctionSupportedOnWindows11UnsupportedOnWindows12();
                unsupportedOnWindows.FunctionSupportedOnWindows11UnsupportedOnWindows12SupportedOnWindows13();

                var unsupportedOnBrowser = [|new TypeUnsupportedOnBrowser()|]; // This call site is reachable on: 'Browser'. 'TypeUnsupportedOnBrowser' is unsupported on: 'browser'.
                [|unsupportedOnBrowser.FunctionSupportedOnBrowser()|]; // warn for unsupported browser type

                var unsupportedOnWindowsSupportedOnWindows11 = new TypeUnsupportedOnWindowsSupportedOnWindows11(); 
                unsupportedOnWindowsSupportedOnWindows11.FunctionUnsupportedOnWindows12();
                unsupportedOnWindowsSupportedOnWindows11.FunctionUnsupportedOnWindows12SupportedOnWindows13();

                var unsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12 = new TypeUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12();
                unsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12.FunctionSupportedOnWindows13();
            }

            if (OperatingSystem.IsWindows())
            {
                var withoutAttributes = new TypeWithoutAttributes();
                [|withoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11()|]; // This call site is reachable on: 'Windows' all versions. 'TypeWithoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11()' is supported on: 'windows' 11.0 and later.
                [|withoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12()|]; // This call site is reachable on: 'Windows' all versions. 'TypeWithoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12()' is supported on: 'windows' from version 11.0 to 12.0.
                [|withoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12SupportedOnWindows13()|]; // This call site is reachable on: 'Windows' all versions. 'TypeWithoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12SupportedOnWindows13()' is supported on: 'windows' from version 11.0 to 12.0.

                var unsupportedOnWindows = [|new TypeUnsupportedOnWindows()|]; // This call site is reachable on: 'Windows'. 'TypeUnsupportedOnWindows' is unsupported on: 'windows'.
                [|unsupportedOnWindows.FunctionSupportedOnWindows11()|];  // should only warn for unsupported type, function attribute ignored

                var unsupportedOnBrowser = new TypeUnsupportedOnBrowser();
                unsupportedOnBrowser.FunctionSupportedOnBrowser();

                var unsupportedOnWindowsSupportedOnWindows11 = [|new TypeUnsupportedOnWindowsSupportedOnWindows11()|]; // This call site is reachable on: 'Windows' all versions. 'TypeUnsupportedOnWindowsSupportedOnWindows11' is supported on: 'windows' 11.0 and later.
                [|unsupportedOnWindowsSupportedOnWindows11.FunctionUnsupportedOnWindows12SupportedOnWindows13()|];

                var unsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12 = [|new TypeUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12()|]; // This call site is reachable on: 'Windows' all versions. 'TypeUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12' is supported on: 'windows' from version 11.0 to 12.0.
            }
        }
    }
}" + TargetTypesForTest;

            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task MoreGuardsAroundUnSupported()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

namespace PlatformCompatDemo.SupportedUnupported
{
    class Caller
    {
        public static void UnsupportedSingleCondition()
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10))
            {
                var unsupported = new TypeWithoutAttributes();
                [|unsupported.FunctionUnsupportedOnWindows()|]; // This call site is reachable on all platforms. 'TypeWithoutAttributes.FunctionUnsupportedOnWindows()' is unsupported on: 'windows'.
                [|unsupported.FunctionUnsupportedOnBrowser()|];
                unsupported.FunctionUnsupportedOnWindows10();
                [|unsupported.FunctionUnsupportedOnWindowsAndBrowser()|]; // This call site is reachable on all platforms. 'TypeWithoutAttributes.FunctionUnsupportedOnWindowsAndBrowser()' is unsupported on: 'windows', 'browser'.
                [|unsupported.FunctionUnsupportedOnWindows10AndBrowser()|]; // This call site is reachable on all platforms. 'TypeWithoutAttributes.FunctionUnsupportedOnWindows10AndBrowser()' is unsupported on: 'browser'.

                var unsupportedOnWindows = [|new TypeUnsupportedOnWindows()|];
                [|unsupportedOnWindows.FunctionUnsupportedOnWindows11()|];

                var unsupportedOnBrowser = [|new TypeUnsupportedOnBrowser()|];
                [|unsupportedOnBrowser.FunctionUnsupportedOnWindows()|]; // This call site is reachable on all platforms. 'TypeUnsupportedOnBrowser.FunctionUnsupportedOnWindows()' is unsupported on: 'browser', 'windows'.
                [|unsupportedOnBrowser.FunctionUnsupportedOnWindows10()|]; // This call site is reachable on all platforms. 'TypeUnsupportedOnBrowser.FunctionUnsupportedOnWindows10()' is unsupported on: 'browser'.

                var unsupportedOnWindows10 = new TypeUnsupportedOnWindows10();
                [|unsupportedOnWindows10.FunctionUnsupportedOnBrowser()|];
                unsupportedOnWindows10.FunctionUnsupportedOnWindows11(); // We should ignore above version of unsupported if there is no supported in between
                [|unsupportedOnWindows10.FunctionUnsupportedOnWindows11AndBrowser()|];

                var unsupportedOnWindowsAndBrowser = [|new TypeUnsupportedOnWindowsAndBrowser()|];
                [|unsupportedOnWindowsAndBrowser.FunctionUnsupportedOnWindows11()|]; // This call site is reachable on all platforms. 'TypeUnsupportedOnWindowsAndBrowser.FunctionUnsupportedOnWindows11()' is unsupported on: 'windows', 'browser'.

                var unsupportedOnWindows10AndBrowser = [|new TypeUnsupportedOnWindows10AndBrowser()|];
                [|unsupportedOnWindows10AndBrowser.FunctionUnsupportedOnWindows11()|];
            }
        }

        public static void UnsupportedWithAnd()
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10) && !OperatingSystem.IsBrowser())
            {
                var unsupported = new TypeWithoutAttributes();
                [|unsupported.FunctionUnsupportedOnWindows()|]; // This call site is reachable on all platforms. 'TypeWithoutAttributes.FunctionUnsupportedOnWindows()' is unsupported on: 'windows'.
                unsupported.FunctionUnsupportedOnBrowser();
                unsupported.FunctionUnsupportedOnWindows10();
                [|unsupported.FunctionUnsupportedOnWindowsAndBrowser()|];
                unsupported.FunctionUnsupportedOnWindows10AndBrowser();

                var unsupportedOnWindows = [|new TypeUnsupportedOnWindows()|];
                [|unsupportedOnWindows.FunctionUnsupportedOnBrowser()|];
                [|unsupportedOnWindows.FunctionUnsupportedOnWindows11()|];
                [|unsupportedOnWindows.FunctionUnsupportedOnWindows11AndBrowser()|]; // This call site is reachable on all platforms. 'TypeUnsupportedOnWindows.FunctionUnsupportedOnWindows11AndBrowser()' is unsupported on: 'windows'.

                var unsupportedOnBrowser = new TypeUnsupportedOnBrowser();
                [|unsupportedOnBrowser.FunctionUnsupportedOnWindows()|]; // This call site is reachable on all platforms. 'TypeUnsupportedOnBrowser.FunctionUnsupportedOnWindows()' is unsupported on: 'windows'.
                unsupportedOnBrowser.FunctionUnsupportedOnWindows10();

                var unsupportedOnWindows10 = new TypeUnsupportedOnWindows10();
                unsupportedOnWindows10.FunctionUnsupportedOnBrowser();
                unsupportedOnWindows10.FunctionUnsupportedOnWindows11();
                unsupportedOnWindows10.FunctionUnsupportedOnWindows11AndBrowser();

                var unsupportedOnWindowsAndBrowser = [|new TypeUnsupportedOnWindowsAndBrowser()|]; // This call site is reachable on all platforms. 'TypeUnsupportedOnWindowsAndBrowser' is unsupported on: 'windows'.
                [|unsupportedOnWindowsAndBrowser.FunctionUnsupportedOnWindows11()|]; // This call site is reachable on all platforms. 'TypeUnsupportedOnWindowsAndBrowser.FunctionUnsupportedOnWindows11()' is unsupported on: 'windows'.

                var unsupportedOnWindows10AndBrowser = new TypeUnsupportedOnWindows10AndBrowser();
                unsupportedOnWindows10AndBrowser.FunctionUnsupportedOnWindows11();

                var unsupportedCombinations = new TypeUnsupportedOnBrowser();
                unsupportedOnBrowser.FunctionSupportedOnBrowser();
            }
        }

        public static void UnsupportedCombinations()
        {
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsBrowser())
            {
                var withoutAttributes = new TypeWithoutAttributes();
                withoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11();
                withoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12();
                withoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12SupportedOnWindows13();

                var unsupportedOnWindows = new TypeUnsupportedOnWindows();
                unsupportedOnWindows.FunctionSupportedOnWindows11();
                unsupportedOnWindows.FunctionSupportedOnWindows11UnsupportedOnWindows12();
                unsupportedOnWindows.FunctionSupportedOnWindows11UnsupportedOnWindows12SupportedOnWindows13();

                var unsupportedOnBrowser = new TypeUnsupportedOnBrowser();
                unsupportedOnBrowser.FunctionSupportedOnBrowser();

                var unsupportedOnWindowsSupportedOnWindows11 = new TypeUnsupportedOnWindowsSupportedOnWindows11();
                unsupportedOnWindowsSupportedOnWindows11.FunctionUnsupportedOnWindows12();
                unsupportedOnWindowsSupportedOnWindows11.FunctionUnsupportedOnWindows12SupportedOnWindows13();

                var unsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12 = new TypeUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12();
                unsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12.FunctionSupportedOnWindows13();
            }
        }
    }
}" + TargetTypesForTest;

            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task GuardsAroundUnsupported()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

namespace PlatformCompatDemo.Bugs.GuardsAroundUnsupported
{
    class Caller
    {
        public static void TestWithGuardMethods()
        {
            if (!OperatingSystem.IsWindows())
            {
                Target.UnsupportedInWindows();
                Target.UnsupportedInWindows10();
                [|Target.UnsupportedOnBrowser()|]; // row 15 This call site is reachable on all platforms. 'Target.UnsupportedOnBrowser()' is unsupported on: 'browser'.
                [|Target.UnsupportedOnWindowsAndBrowser()|]; // This call site is reachable on all platforms. 'Target.UnsupportedOnWindowsAndBrowser()' is unsupported on: 'browser'.
                [|Target.UnsupportedOnWindows10AndBrowser()|]; // This call site is reachable on all platforms. 'Target.UnsupportedOnWindows10AndBrowser()' is unsupported on: 'browser'.
            }

            if (!OperatingSystem.IsWindowsVersionAtLeast(10))
            {
                [|Target.UnsupportedInWindows()|]; // This call site is reachable on all platforms. 'Target.UnsupportedInWindows()' is unsupported on: 'windows'.
                Target.UnsupportedInWindows10();
                [|Target.UnsupportedOnBrowser()|]; // This call site is reachable on all platforms. 'Target.UnsupportedOnBrowser()' is unsupported on: 'browser'.
                [|Target.UnsupportedOnWindowsAndBrowser()|]; // This call site is reachable on all platforms. 'Target.UnsupportedOnWindowsAndBrowser()' is unsupported on: 'windows', 'browser'.
                [|Target.UnsupportedOnWindows10AndBrowser()|]; // expected diagnostic - browser unsupported
            }

            if (OperatingSystem.IsWindows())
            {
                [|Target.UnsupportedInWindows()|]; // This call site is reachable on: 'Windows'. 'Target.UnsupportedInWindows()' is unsupported on: 'windows'.
                [|Target.UnsupportedInWindows10()|]; // expected diagnostic - windows 10 unsupported
                Target.UnsupportedOnBrowser();
                [|Target.UnsupportedOnWindowsAndBrowser()|]; // expected diagnostic - windows unsupported
                [|Target.UnsupportedOnWindows10AndBrowser()|]; // This call site is reachable on: 'Windows' all versions. 'Target.UnsupportedOnWindows10AndBrowser()' is unsupported on: 'windows' 10.0 and later.
            }

            if (OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(10))
            {
                [|Target.UnsupportedInWindows()|]; // This call site is reachable on: 'Windows'. 'Target.UnsupportedInWindows()' is unsupported on: 'windows'.
                Target.UnsupportedInWindows10();
                Target.UnsupportedOnBrowser(); 
                [|Target.UnsupportedOnWindowsAndBrowser()|]; // This call site is reachable on: 'Windows'. 'Target.UnsupportedOnWindowsAndBrowser()' is unsupported on: 'windows'.
                Target.UnsupportedOnWindows10AndBrowser();
            }

            if (OperatingSystem.IsBrowser())
            {
                Target.UnsupportedInWindows();
                Target.UnsupportedInWindows10();
                [|Target.UnsupportedOnBrowser()|}; // This call site is reachable on: 'Browser'. 'Target.UnsupportedOnBrowser()' is unsupported on: 'browser'.
                [|Target.UnsupportedOnWindowsAndBrowser()|]; // expected diagnostic - browser unsupported, same
                [|Target.UnsupportedOnWindows10AndBrowser()|]; // This call site is reachable on: 'Browser'. 'Target.UnsupportedOnWindows10AndBrowser()' is unsupported on: 'browser'.
            }
        }
    }

    class Target
    {
        [UnsupportedOSPlatform(""windows"")]
        public static void UnsupportedInWindows() { }

        [UnsupportedOSPlatform(""windows10.0"")]
        public static void UnsupportedInWindows10() { }

        [UnsupportedOSPlatform(""browser"")]
        public static void UnsupportedOnBrowser() { }

        [UnsupportedOSPlatform(""windows""), UnsupportedOSPlatform(""browser"")]
        public static void UnsupportedOnWindowsAndBrowser() { }

        [UnsupportedOSPlatform(""windows10.0""), UnsupportedOSPlatform(""browser"")]
        public static void UnsupportedOnWindows10AndBrowser() { }
    }
}";

            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task SupportedUnsupportedRange_GuardedWithAnd()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    public void Api_Usage()
    {
        if (OperatingSystem.IsIOSVersionAtLeast(12,0) &&
           !OperatingSystem.IsIOSVersionAtLeast(14,0))
        {
            Api();
        }
        [|Api()|]; // This call site is reachable on all platforms. 'Test.Api()' is only supported on: 'ios' from version 12.0 to 14.0.
    }

    [SupportedOSPlatform(""ios12.0"")]
    [UnsupportedOSPlatform(""ios14.0"")]
    void Api() { }
}";

            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task Unsupported_GuardedWith_IsOsNameMethods()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    void M1()
    {
        if(!OperatingSystem.IsBrowser())
        {
            NotForBrowser();
            [|NotForIos12OrLater()|];
        }
        else
        {
            NotForIos12OrLater();
            [|NotForBrowser()|];
        }

        if(OperatingSystem.IsOSPlatform(""Browser""))
        {
            [|NotForBrowser()|];
        }
        else
        {
            NotForBrowser();
        }
        
        if(OperatingSystem.IsIOS())
        {
            [|NotForIos12OrLater()|];
        }
        else
        {
            NotForIos12OrLater();
        }

        if(OperatingSystem.IsIOSVersionAtLeast(12,1))
        {
            [|NotForIos12OrLater()|];
        }
        else
        {
            NotForIos12OrLater();
        }

        if(OperatingSystem.IsIOS() && !OperatingSystem.IsIOSVersionAtLeast(12,0))
        {
            NotForIos12OrLater();
        }
        else
        {
            [|NotForIos12OrLater()|];
        }
    }

    [UnsupportedOSPlatform(""browser"")]
    void NotForBrowser() { }

    [UnsupportedOSPlatform(""ios12.1"")]
    void NotForIos12OrLater() { }
}";

            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact, WorkItem(4190, "https://github.com/dotnet/roslyn-analyzers/issues/4190")]
        public async Task Unsupported_GuardedWith_DebugAssert_IsOsNameMethods()
        {
            var source = @"
using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        Debug.Assert(!OperatingSystem.IsBrowser());

        NotForBrowser();
        [|NotForIos12OrLater()|];
    }

    void M2()
    {
        Debug.Assert(OperatingSystem.IsBrowser());

        NotForIos12OrLater();
        [|NotForBrowser()|];
    }

    void M3()
    {
        Debug.Assert(OperatingSystem.IsOSPlatform(""Browser""));
        [|NotForBrowser()|];
    }

    void M4()
    {
        Debug.Assert(!OperatingSystem.IsOSPlatform(""Browser""));
        NotForBrowser();
    }

    void M5()
    {
        Debug.Assert(OperatingSystem.IsIOS());
        [|NotForIos12OrLater()|];
    }

    void M6()
    {
        Debug.Assert(!OperatingSystem.IsIOS());
        NotForIos12OrLater();
    }

    void M7()
    {
        Debug.Assert(OperatingSystem.IsIOSVersionAtLeast(12,1));
        [|NotForIos12OrLater()|];
    }

    void M8()
    {
        Debug.Assert(!OperatingSystem.IsIOSVersionAtLeast(12,1));
        NotForIos12OrLater();
    }

    void M9()
    {
        Debug.Assert(OperatingSystem.IsIOS());
        Debug.Assert(!OperatingSystem.IsIOSVersionAtLeast(12,0));
        
        NotForIos12OrLater();
    }

    void M10()
    {
        Debug.Assert(!(OperatingSystem.IsIOS() && !OperatingSystem.IsIOSVersionAtLeast(12,0)));
        [|NotForIos12OrLater()|];
    }

    [UnsupportedOSPlatform(""browser"")]
    void NotForBrowser() { }

    [UnsupportedOSPlatform(""ios12.1"")]
    void NotForIos12OrLater() { }
}";
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        public static IEnumerable<object[]> OperatingSystem_IsOsNameVersionAtLeast_MethodsTestData()
        {
            yield return new object[] { "Windows", "IsWindows", "10,1", true };
            yield return new object[] { "windows11.0", "IsWindows", "10,1,2,3", false };
            yield return new object[] { "WINDOWS10.1.2", "IsWindows", "10,1,2", true };
            yield return new object[] { "FreeBSD", "IsFreeBSD", "10", true };
            yield return new object[] { "FreeBSD12.0", "IsFreeBSD", "10,1,2", false };
            yield return new object[] { "freebsd10.1.2", "IsFreeBSD", "10,1,2", true };
            yield return new object[] { "Android", "IsAndroid", "10,1,2", true };
            yield return new object[] { "android11.0", "IsAndroid", "10,1,2", false };
            yield return new object[] { "Android10.1.2", "IsAndroid", "10,1,2", true };
            yield return new object[] { "IOS", "IsIOS", "10,1,2", true };
            yield return new object[] { "ios12.0", "IsIOS", "10,1,2", false };
            yield return new object[] { "iOS10.1.2", "IsIOS", "10,1,2", true };
            yield return new object[] { "MacOS", "IsMacOS", "10,1,2", true };
            yield return new object[] { "macOS14.0", "IsMacOS", "10,1,2", false };
            yield return new object[] { "macos10.1.2", "IsMacOS", "10,1,2", true };
            yield return new object[] { "TvOS", "IsTvOS", "10,1,2", true };
            yield return new object[] { "tvOS13.0", "IsTvOS", "10,1,2", false };
            yield return new object[] { "Tvos10.1", "IsTvOS", "10,1,2", true };
            yield return new object[] { "watchOS", "IsWatchOS", "10,1,2", true };
            yield return new object[] { "WatchOS14.0", "IsWatchOS", "10,1,2", false };
            yield return new object[] { "watchos10.0", "IsWatchOS", "10,1,2", true };
        }

        [Theory]
        [MemberData(nameof(OperatingSystem_IsOsNameVersionAtLeast_MethodsTestData))]
        public async Task GuardedWith_IsOsNameVersionAtLeast_SimpleIfElse(string osName, string isOsMethod, string version, bool versionMatch)
        {
            var match = versionMatch ? "OsSpecificMethod()" : "[|OsSpecificMethod()|]";
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    void M1()
    {
        if(OperatingSystem." + isOsMethod + @"VersionAtLeast(" + version + @"))
        {
            " + match + @";
        }
        else
        {
            [|OsSpecificMethod()|];
        }
    }

    [SupportedOSPlatform(""" + osName + @""")]
    void OsSpecificMethod() { }
}";

            await VerifyAnalyzerAsyncCs(source);
        }

        public static IEnumerable<object[]> OperatingSystem_IsOsName_MethodsTestData()
        {
            yield return new object[] { "Windows", "IsWindows" };
            yield return new object[] { "WINDOWS", "IsWindows" };
            yield return new object[] { "windows", "IsWindows" };
            yield return new object[] { "LinuX", "IsLinux" };
            yield return new object[] { "linux", "IsLinux" };
            yield return new object[] { "Browser", "IsBrowser" };
            yield return new object[] { "browser", "IsBrowser" };
            yield return new object[] { "FreeBSD", "IsFreeBSD" };
            yield return new object[] { "freebsd", "IsFreeBSD" };
            yield return new object[] { "Android", "IsAndroid" };
            yield return new object[] { "android", "IsAndroid" };
            yield return new object[] { "IOS", "IsIOS" };
            yield return new object[] { "Ios", "IsIOS" };
            yield return new object[] { "ios", "IsIOS" };
            yield return new object[] { "MacOS", "IsMacOS" };
            yield return new object[] { "macOS", "IsMacOS" };
            yield return new object[] { "macos", "IsMacOS" };
            yield return new object[] { "TvOS", "IsTvOS" };
            yield return new object[] { "tvOS", "IsTvOS" };
            yield return new object[] { "watchOS", "IsWatchOS" };
            yield return new object[] { "WatchOS", "IsWatchOS" };
            yield return new object[] { "watchos", "IsWatchOS" };
        }

        [Theory]
        [MemberData(nameof(OperatingSystem_IsOsName_MethodsTestData))]
        public async Task GuardedWith_IsOsNameMethods_SimpleIfElse(string osName, string isOsMethod)
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    void M1()
    {
        if(OperatingSystem." + isOsMethod + @"())
        {
            OsSpecificMethod();
        }
        else
        {
            [|OsSpecificMethod()|];
        }
    }

    [SupportedOSPlatform(""" + osName + @""")]
    void OsSpecificMethod() { }
}";

            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedWith_OperatingSystem_IsOSPlatform_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    void M1()
    {
        if(OperatingSystem.IsOSPlatform(""Windows""))
        {
            M2();
        }
        else
        {
            [|M2()|];
        }

        if(OperatingSystem.IsOSPlatform(""Windows8.0""))
        {
            M2();
        }
        else
        {
            [|M2()|];
        }
    }

    [SupportedOSPlatform(""windows"")]
    void M2() { }
}";

            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedWith_RuntimeInformation_IsOSPlatform_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            M2();
        }
        else
        {
            [|M2()|];
        }
    }

    [SupportedOSPlatform(""Windows"")]
    void M2() { }
}";

            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact, WorkItem(4119, "https://github.com/dotnet/roslyn-analyzers/issues/4119")]
        public async Task GuardedWith_RuntimeInformation_IsOSPlatform_OSPlatformCreate_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Create(""Windows"")))
        {
            M2();
        }
        else
        {
            [|M2()|];
        }
    }

    [SupportedOSPlatform(""Windows"")]
    void M2() { }
}";

            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact, WorkItem(4119, "https://github.com/dotnet/roslyn-analyzers/issues/4119")]
        public async Task GuardedWith_RuntimeInformation_IsOSPlatform_OSPlatformCreate_ValueCachedInLocal_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

class Test
{
    void M1(bool isWindows)
    {
        var windowsPlatform = OSPlatform.Create(""Windows"");
        if(RuntimeInformation.IsOSPlatform(windowsPlatform))
        {
            M2();
        }
        else
        {
            [|M2()|];
        }
    }

    [SupportedOSPlatform(""Windows"")]
    void M2() { }
}";

            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact, WorkItem(4119, "https://github.com/dotnet/roslyn-analyzers/issues/4119")]
        public async Task GuardedWith_RuntimeInformation_IsOSPlatform_OSPlatformCreate_MultipleValuesCachedWithConditionalLogic()
        {
            var source = @"
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

class Test
{
    void M1(bool isWindows, OSPlatform? unknown)
    {
        var windowsPlatform = OSPlatform.Create(""Windows"");
        var linuxPlatform = OSPlatform.Create(""Linux"");
        var platform = isWindows ? windowsPlatform : linuxPlatform;
        if(RuntimeInformation.IsOSPlatform(platform))
        {
            M2();
        }
        else
        {
            [|M2()|];
        }

        if(RuntimeInformation.IsOSPlatform(windowsPlatform))
        {
            M2();
        }
        else
        {
            [|M2()|];
        }

        if (unknown.HasValue)
        {
            platform = unknown.Value;
        }
        else
        {
            platform = OSPlatform.Create(""Browser"");
        }

        if(RuntimeInformation.IsOSPlatform(platform))
        {
            [|M2()|];
        }
        else
        {
            [|M2()|];
        }
    }

    [SupportedOSPlatform(""Windows"")]
    [SupportedOSPlatform(""Linux"")]
    void M2() { }
}";

            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedCalled_SimpleIfElse_VersionNotMatch_Warns()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

[SupportedOSPlatform(""windows7.0"")]
static class Program
{
    public static void Main()
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            [|WindowsSpecificApis.WindowsOnlyMethod()|];
        }
        else
        {
            [|WindowsSpecificApis.WindowsOnlyMethod()|];
        }
    }
}

public class WindowsSpecificApis
{
    [SupportedOSPlatform(""windows10.1.2.3"")]
    public static void WindowsOnlyMethod() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task ReintroducingApiSupport_Guarded_NotWarn()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

[SupportedOSPlatform(""windows"")]
static class Program
{
    public static void Main()
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            Some.WindowsSpecificApi();
        }
        else
        {
            {|#0:Some.WindowsSpecificApi()|}; // This call site is reachable on: 'windows' all versions. 'Some.WindowsSpecificApi()' is supported on: 'windows' 10.0 and later.
        }
    }
}

static class Some
{
    [UnsupportedOSPlatform(""windows"")]
    [SupportedOSPlatform(""windows10.0"")]
    public static void WindowsSpecificApi() { }
}
";

            await VerifyAnalyzerAsyncCs(source,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsReachable).WithLocation(0).
                WithArguments("Some.WindowsSpecificApi()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"),
                GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows")));
        }

        [Fact]
        public async Task GuardedCalled_SimpleIf_NotWarns()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        [|M2()|];
        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 1, 2, 3))
            M2();
    }
    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);

            var vbSource = @"
Imports System.Runtime.Versioning
Imports System

Public Class Test
    Public Sub M1()
        [|M2()|]
        If OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 1, 2, 3) Then M2()
    End Sub

    <SupportedOSPlatform(""Windows10.1.2.3"")>
    Public Sub M2()
    End Sub
End Class";
            await VerifyAnalyzerAsyncVb(vbSource);
        }

        [Fact]
        public async Task GuardedCall_MultipleSimpleIfTests()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        [|M2()|];
        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 1, 2, 3))
            M2();
        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Linux"", 10, 1, 2, 3))
            [|M2()|];
        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2))
            M2();
        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 8, 1, 2, 3))
            [|M2()|];        
    }
    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedWith_IsOSPlatformVersionAtLeast_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    void M1()
    {
        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
        {
            M2();
        }
        else
        {
            [|M2()|];
        }
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    void M2() { }
}";

            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedWith_AlternativeOf_IsOSPlatformEarlierThan()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    [SupportedOSPlatform(""Windows"")]
    void M1()
    {
        if(OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(10, 0))
        {
            [|M2()|];
            M3();
        }
        else
        {
            [|M2()|];
            [|M3()|];
        }
    }

    [SupportedOSPlatform(""MacOs12.2.3"")]
    void M2() { }

    [UnsupportedOSPlatform(""Windows10.0"")]
    void M3() { }
}";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedWith_Unsupported_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    [SupportedOSPlatform(""Windows"")]
    void M1()
    {
        if(OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(10, 1, 2, 3))
        {
            [|M2()|];
            M3();
        }
        else
        {
            [|M2()|];
            [|M3()|];
        }

        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"",10,1,3))
        {
            [|M3()|];
            M2();
        }
        else
        {
            [|M2()|];
            [|M3()|];
        }
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    void M2() { }

    [UnsupportedOSPlatform(""Windows10.1.2.3"")]
    void M3() { }
}";
            await VerifyAnalyzerAsyncCs(source);

            var vbSource = @"
Imports System.Runtime.Versioning
Imports System

Class Test
    <SupportedOSPlatform(""Windows"")>
    Private Sub M1()
        If OperatingSystem.IsWindows() AndAlso Not OperatingSystem.IsWindowsVersionAtLeast(10, 1, 2, 3) Then
            [|M2()|]
            M3()
        Else
            [|M2()|]
            [|M3()|]
        End If

        If OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"",10,1,3) Then
            [|M3()|]
            M2()
        Else
            [|M2()|]
            [|M3()|]
        End If
    End Sub

    <SupportedOSPlatform(""Windows10.1.2.3"")>
    Private Sub M2()
    End Sub

    <UnsupportedOSPlatform(""Windows10.1.2.3"")>
    Private Sub M3()
    End Sub
End Class";
            await VerifyAnalyzerAsyncVb(vbSource);
        }

        [Fact]
        public async Task OsDependentEnumValue_GuardedCall_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test2
{
    public void M1()
    {
        PlatformEnum val = [|PlatformEnum.Windows10|];
        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10))
        {
            M2(PlatformEnum.Windows10);
        }
        else
        {
            M2([|PlatformEnum.Windows10|]);
        }
        M2([|PlatformEnum.Linux48|]);
        M2(PlatformEnum.NoPlatform);
    }
    public PlatformEnum M2(PlatformEnum option)
    {
        return option;
    }
}

public enum PlatformEnum
{
    [SupportedOSPlatform(""Windows10.0"")]
    Windows10,
    [SupportedOSPlatform(""Linux4.8"")]
    Linux48,
    NoPlatform
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task OsDependentProperty_GuardedCall_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    [UnsupportedOSPlatform(""Windows8.1"")]
    public string RemovedProperty { get; set;}

    public static bool WindowsOnlyPropertyGetter
    {
        [SupportedOSPlatform(""windows"")]
        get { return true; }
        set { }
    }
    
    public void M1()
    {
        if(OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(8, 0, 19222)) 
        {
            WindowsOnlyPropertyGetter = true;
            var val = WindowsOnlyPropertyGetter;
            RemovedProperty = ""Hello"";
            string s = RemovedProperty;
            M2(RemovedProperty);
        }
        else
        {
            WindowsOnlyPropertyGetter = true;
            var val = [|WindowsOnlyPropertyGetter|];
            [|RemovedProperty|] = ""Hello"";
            string s = [|RemovedProperty|];
            M2([|RemovedProperty|]);
        }
    }

    public string M2(string option)
    {
        return option;
    }
}
";
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact, WorkItem(4105, "https://github.com/dotnet/roslyn-analyzers/issues/4105")]
        public async Task OsDependentPropertyWithInitializer_NoDiagnostic()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class C
{
    public static object Property { get; } = OperatingSystem.IsWindows() ? null : new A();
}

[UnsupportedOSPlatform(""windows"")]
class A { }
";
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact, WorkItem(4105, "https://github.com/dotnet/roslyn-analyzers/issues/4105")]
        public async Task OsDependentPropertyWithInitializer_Diagnostic()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class C
{
    public static object Property { get; } = OperatingSystem.IsWindows() ? [|new A()|] : null;
}

[UnsupportedOSPlatform(""windows"")]
class A { }
";
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact, WorkItem(4105, "https://github.com/dotnet/roslyn-analyzers/issues/4105")]
        public async Task OsDependentFieldWithInitializer_NoDiagnostic()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class C
{
    private static object _field = OperatingSystem.IsWindows() ? null : new A();
}

[UnsupportedOSPlatform(""windows"")]
class A { }
";
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact, WorkItem(4105, "https://github.com/dotnet/roslyn-analyzers/issues/4105")]
        public async Task OsDependentFieldWithInitializer_Diagnostic()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class C
{
    private static object _field = OperatingSystem.IsWindows() ? [|new A()|] : null;
}

[UnsupportedOSPlatform(""windows"")]
class A { }
";
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task OsDependentConstructorOfClass_GuardedCall_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2))
        {
            C instance = new C();
            instance.M2();
        }
        else
        {   
            C instance2 = [|new C()|];
            instance2.M2();
        }
    }
}

public class C
{
    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public C() { }

    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task ConstructorAndMethodOfOsDependentClass_GuardedCall_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2))
        {
            OsDependentClass odc = new OsDependentClass();
            odc.Method2();
        }
        else
        {
            OsDependentClass odc2 = [|new OsDependentClass()|];
            [|odc2.Method2()|];
        }
    }
}
[SupportedOSPlatform(""Windows10.1.2.3"")]
public class OsDependentClass
{
    public void Method2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);

            var vbSource = @"
Imports System.Runtime.Versioning
Imports System

Public Class Test
    Public Sub M1()
        If OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2) Then
            Dim odc As OsDependentClass = New OsDependentClass()
            odc.M2()
        Else
            Dim odc2 As OsDependentClass = [|New OsDependentClass()|]
            [|odc2.M2()|]
        End If
    End Sub
End Class

<SupportedOSPlatform(""Windows10.1.2.3"")>
Public Class OsDependentClass
    Public Sub M2()
    End Sub
End Class";
            await VerifyAnalyzerAsyncVb(vbSource);
        }

        [Fact]
        public async Task LocalFunctionCallsOsDependentMember_GuardedCall_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        void Test()
        {
            if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2, 1))
            {
                M2();
            }
            else
            {
                [|M2()|];
            }
        }
        Test();
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LocalFunctionCallsPlatformDependentMember_InvokedFromDifferentContext()
        {
            var source = @"
using System.Runtime.Versioning;
using System;
public class Test
{
    void M()
    {
        LocalM();
        if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2))
        {
            LocalM();
        }
        return;

        void LocalM()
        {
            [|WindowsOnlyMethod()|];
           
            if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2))
            {
                WindowsOnlyMethod();
            }

            if (OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(10,0))
            {
                UnsupportedWindows10();
            }
            else
            {
                [|UnsupportedWindows10()|];
            }
        }
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void WindowsOnlyMethod() { }
    [UnsupportedOSPlatform(""Windows10.0"")]
    public void UnsupportedWindows10() { }
}";
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task LocalFunctionCallsPlatformDependentMember_InvokedFromNotGuardedDifferentContext()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    void M()
    {
        LocalM();

        if (!OperatingSystem.IsOSPlatformVersionAtLeast(""Linux"", 10, 2))
        {
            LocalM();
        }

        LocalM();
        return;

        void LocalM()
        {
            [|WindowsOnlyMethod()|];

            if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2))
            {
                WindowsOnlyMethod();
            }
            else
            {
                [|WindowsOnlyMethod()|];
            }

            if (OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(10,0))
            {
                UnsupportedWindows10();
            }
            else
            {
                [|UnsupportedWindows10()|];
            }
        }
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void WindowsOnlyMethod() { }

    [UnsupportedOSPlatform(""Windows10.0"")]
    public void UnsupportedWindows10() { }
}
";
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task LocalFunctionCallsPlatformDependentMember_InvokedFromGuardedDifferentContext()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    void M()
    {
        if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2))
        {
            LocalM();
        }

        if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2))
        {
            LocalM();
        }

        return;

        void LocalM()
        {
            WindowsOnlyMethod();

            if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2))
            {
                WindowsOnlyMethod();
            }
            else
            {
                WindowsOnlyMethod();
            }

            if (OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(10,0))
            {
                UnsupportedWindows10();
            }
            else
            {
                [|UnsupportedWindows10()|];
            }
        }
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void WindowsOnlyMethod() { }

    [UnsupportedOSPlatform(""Windows10.0"")]
    public void UnsupportedWindows10() { }
}
";
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task LocalFunctionEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        // Warn inside local function escaped as delegate.
        void LocalFunction()
        {
            if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
            {
                M2();
            }
            else
            {
                [|M2()|];
            }
        }

        M3(LocalFunction);
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }

    public void M3(Action a) { a(); }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LocalFunctionEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_02()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        // Warn inside local function escaped as delegate indirectly from another local function.
        void LocalFunction()
        {
            if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
            {
                M2();
            }
            else
            {
                [|M2()|];
            }
        }

        void LocalFunction2()
        {
            // Escaped inside another local function.
            M3(LocalFunction);
        }

        LocalFunction2();
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }

    public void M3(Action a) { a(); }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LocalFunctionEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_03()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        // Warn inside local function escaped as delegate indirectly from a lambda invoked inside a local function.
        void LocalFunction()
        {
            if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
            {
                M2();
            }
            else
            {
                [|M2()|];
            }
        }

        Action a = () =>
        {
            // Escaped inside a lambda invoked inside another local function.
            M3(LocalFunction);
        };

        void LocalFunction2()
        {
            a();
        }

        LocalFunction2();
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }

    public void M3(Action a) { a(); }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LocalFunctionEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_04()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        // Warn inside local function when escaped as delegate + invoked directly from guarded context.
        void LocalFunction()
        {
            [|M2()|];
        }

        // Escaped as delegate, can potentially be invoked from unguarded context.
        M3(LocalFunction);

        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
        {
            // Invoked directly from guarded context.
            LocalFunction();
        }
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }

    public void M3(Action a) { a(); }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LocalFunctionMultipleCallsOsDependentMember_MixedGuardedCalls()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        // Do not warn 'WindowsOnly' inside local function, all calls from guarded context.
        // Warn on 'Windows10OrLaterOnly' inside local function, some calls are from unguarded context.
        void LocalFunction()
        {
            WindowsOnly();
            [|Windows10OrLaterOnly()|];
        }

        void LocalFunction2()
        {
            LocalFunction();
        }

        if(OperatingSystem.IsWindows())
        {
            if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
            {
                // Invoked multiple times directly from guarded context.
                LocalFunction();
                LocalFunction();

                // Invoked indirectly via another local function, but from guarded context.
                LocalFunction2();
            }

            // No Windows10 guard.
            LocalFunction();
        }
    }

    [SupportedOSPlatform(""Windows"")]
    public void WindowsOnly() { }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void Windows10OrLaterOnly() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LocalFunctionMultipleCalls_DifferentOrder_OsDependentMember_MixedGuardedCalls()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        void LocalFunction()
        {
            [|WindowsOnly()|];
        }

        // Unguarded call before guarded call - should warn.
        LocalFunction();
        if(OperatingSystem.IsWindows())
        {
            LocalFunction();
        }
    }

    public void M2()
    {
        void LocalFunction()
        {
            [|WindowsOnly()|];
        }

        // Guarded call before unguarded call - should warn.
        LocalFunction();
        if(OperatingSystem.IsWindows())
        {
            LocalFunction();
        }
    }

    [SupportedOSPlatform(""Windows"")]
    public void WindowsOnly() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LocalFunctionWithUnrelatedLocalFunctionCallsOsDependentMember_GuardedCalls()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        // Do not warn inside local function, all calls from guarded context.
        void LocalFunction()
        {
            M2();
        }

        void LocalFunction2()
        {
        }

        if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
        {
            LocalFunction();
        }

        // Unrelated 'LocalFunction2' call from unguarded context should not affect analysis of 'LocalFunction'
        LocalFunction2();
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LocalFunctionUnusedCallsOsDependentMember_GuardedCalls_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        // Do not warn inside unused local function.
        void LocalFunction()
        {
            if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
            {
                M2();
            }
            else
            {
                [|M2()|];
            }
        };
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LambdaCallsOsDependentMember_GuardedCall_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2, 1))
        {
            void Test() => M2();
            Test();
        }
        else
        {
            void Test() => [|M2()|];
            Test();
        }

        Action action = () =>
        {
            if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2, 1))
            {
                M2();
            }
            else
            {
                [|M2()|];
            }
        };
        action.Invoke();
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact, WorkItem(4090, "https://github.com/dotnet/roslyn-analyzers/issues/4090")]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_DirectlyPassedAsArgument()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        // Warn inside lambda escaped as delegate argument.
        M3(a: () =>
        {
            if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
            {
                M2();
            }
            else
            {
                [|M2()|];
            }
        });
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }

    public void M3(Action a) { a(); }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        // Warn inside lambda escaped as delegate.
        Action a = () =>
        {
            if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
            {
                M2();
            }
            else
            {
                [|M2()|];
            }
        };

        M3(a);
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }

    public void M3(Action a) { a(); }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_02()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        // Warn inside lambda escaped as delegate indirectly from another lambda.
        Action a1 = () =>
        {
            if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
            {
                M2();
            }
            else
            {
                [|M2()|];
            }
        };

        Action a2 = () =>
        {
            // Escaped inside another local function.
            M3(a1);
        };

        a2();
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }

    public void M3(Action a) { a(); }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_03()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        // Warn inside lambda escaped as delegate indirectly from a local function invoked inside a lambda.
        Action a1 = () =>
        {
            if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
            {
                M2();
            }
            else
            {
                [|M2()|];
            }
        };

        void LocalFunction()
        {
            // Escaped inside a local function invoked inside another lambda.
            M3(a1);
        }

        Action a2 = () =>
        {
            LocalFunction();
        };

        a2();
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }

    public void M3(Action a) { a(); }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_04()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    private Action _field;

    public void M1()
    {
        // Warn inside lambda escaped via field.
        Action a = () =>
        {
            if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
            {
                M2();
            }
            else
            {
                [|M2()|];
            }
        };

        _field = a;
        M3();
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }

    public void M3() { _field(); }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_05()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public Action M1()
    {
        // Warn inside lambda escaped via return value.
        Action a = () =>
        {
            if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
            {
                M2();
            }
            else
            {
                [|M2()|];
            }
        };

        return a;
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_06()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        // Warn inside lambda escaped via conversion.
        Action a = () =>
        {
            if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
            {
                M2();
            }
            else
            {
                [|M2()|];
            }
        };

        var x = (object)a;
        M3(x);
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }

    public void M3(object a) { ((Action)a)(); }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_07()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1(out Action result)
    {
        // Warn inside lambda escaped via out argument.
        Action a = () =>
        {
            if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
            {
                M2();
            }
            else
            {
                [|M2()|];
            }
        };

        result = a;
        return;
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_08()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        // Warn inside lambda when escaped as delegate + invoked directly from guarded context.
        Action a = () =>
        {
            [|M2()|];
        };

        // Escaped as delegate, can potentially be invoked from unguarded context.
        M3(a);

        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
        {
            // Invoked directly from guarded context.
            a();
        }
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }

    public void M3(Action a) { a(); }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LambdaUnusedCallsOsDependentMember_GuardedCalls_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        // Warn inside unused lambda.
        Action a = () =>
        {
            if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
            {
                M2();
            }
            else
            {
                [|M2()|];
            }
        };
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LambdaMultipleCallsOsDependentMember_MixedGuardedCalls()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        // Do not warn 'WindowsOnly' inside lambda, all calls from guarded context.
        // Warn on 'Windows10OrLaterOnly' inside lambda, some calls are from unguarded context.
        Action a = () =>
        {
            WindowsOnly();
            [|Windows10OrLaterOnly()|];
        };

        if(OperatingSystem.IsWindows())
        {
            if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
            {
                // Invoked multiple times directly from guarded context.
                a();
                a();

                // Invoked indirectly via local function, but from guarded context.
                LocalFunction();
            }

            // No Windows10 guard.
            a();
        }

        void LocalFunction()
        {
            a();
        }
    }

    [SupportedOSPlatform(""Windows"")]
    public void WindowsOnly() { }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void Windows10OrLaterOnly() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LambdaWithUnrelatedLambdaCallsOsDependentMember_GuardedCalls()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        // Do not warn inside lambda, all calls from guarded context.
        Action a = () =>
        {
            M2();
        };

        Action a2 = () =>
        {
        };

        if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
        {
            a();
        }

        // Unrelated lambda call from unguarded context should not affect analysis of 'a'
        a2();
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LambdaContaingLocalFunctionCallsOsDependentMember_GuardedCall_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        Action a = () =>
        {
            if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2, 1))
            {
                LocalFunction();
            }

            void LocalFunction()
            {
                // Should not warn here as all callsites to 'LocalFunction' are guarded.
                M2();
            }
        };

        a();
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LocalFunctionContainingLambdaCallsOsDependentMember_GuardedCall_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        void LocalFunction()
        {
            Action a = () =>
            {
                // Should not warn here as all callsites to 'a' are guarded.
                M2();
            };

            if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2, 1))
            {
                a();
            }
        }

        LocalFunction();
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact, WorkItem(4209, "https://github.com/dotnet/roslyn-analyzers/issues/4209")]
        public async Task LambdaInvocationWithUnknownTarget_BeforeGuardedCall()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    Func<string> Greetings = () => ""Hi!"";
    
    void M1()
    {
        Greetings();
        if (OperatingSystem.IsBrowser())
        {
            SupportedOnBrowser();
        }
        else
        {
            UnsupportedOnBrowser();
        }
    }

    [SupportedOSPlatform(""browser"")]
    void SupportedOnBrowser() { }

    [UnsupportedOSPlatform(""browser"")]
    void UnsupportedOnBrowser() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task OsDependentEventAccessed_GuardedCall_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public delegate void Del();

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public event Del SampleEvent;

    public void M1()
    {
        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
        {
            SampleEvent += M3;
        }
        else
        {
            [|SampleEvent|] += M4;
        }
    }

    public void M2()
    {
        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
        {
            SampleEvent?.Invoke();
        }
        else
        {
            [|SampleEvent|]?.Invoke();
        }
    }

    public void M3() { }
    public void M4() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task OsDependentMethodAssignedToDelegate_GuardedCall_SimpleIfElse()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public delegate void Del();

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void DelegateMethod() { }

    public void M1()
    {
        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11, 0))
        {
            Del handler = DelegateMethod;
            handler();
        }
        else
        {
            Del handler = [|DelegateMethod|];
            handler();
        }
    }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfElseIfElseTest()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    void M1()
    {
        [|M2()|];

        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
        {
            M2();
        }
        else if(OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(8, 0, 19222))
        {
            [|M2()|];
        }
        else if(OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(12, 0, 19222))
        {
            [|M2()|];
        }
        else if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 12))
        {
            M2();
        }
        else
        {
            [|M2()|];
        }
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    void M2() { }
}";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfElseTestWithNegation()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        [|M2()|];
        if(!OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 1, 2, 3))
            [|M2()|];
        else
            M2();
    }
    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfElseIfElseTestWithNegation()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        [|M2()|];
        if(!OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 1, 2, 3))
            [|M2()|];
        else if(OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(12, 0, 19222))
            M2();
        else
            M2();
    }
    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfTestWithNegationAndReturn()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        [|M2()|];
        if(!OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 1, 2, 3))
            return;
        M2();
    }
    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);

            var vbSource = @"
Imports System.Runtime.Versioning
Imports System

Public Class Test
    Public Sub M1()
        [|M2()|]
        If Not OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 1, 2, 3) Then Return
        M2()
    End Sub

    <SupportedOSPlatform(""Windows10.1.2.3"")>
    Public Sub M2()
    End Sub
End Class";
            await VerifyAnalyzerAsyncVb(vbSource);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfTestWithLogicalAnd()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2) &&
           (OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(12, 0, 19222)))
        {
            M2();
        }

        if((OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(12, 0, 19222)) &&
           OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 12))
        {
            M2();
        }

        if((OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(12, 0, 19222)) &&
           OperatingSystem.IsOSPlatformVersionAtLeast(""Linux"", 12))
        {
            [|M2()|];
        }

        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2) && 1 == 1)
        {
            M2();
        }

        [|M2()|];
    }
    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfElseTestWithLogicalAnd()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        [|M2()|];

        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2) &&
           (OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(12, 0, 19222)))
        {
            M2();
        }
        else
        {
            [|M2()|];
        }

        if((OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(12, 0, 19222)) &&
           OperatingSystem.IsOSPlatformVersionAtLeast(""Linux"", 12))
        {
            [|M2()|];
        }
        else
        {
            [|M2()|];
        }
    }
    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfTestWithLogicalOr()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2) ||
           (OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(12, 0, 19222)))
        {
            [|M2()|];
        }

        if(OperatingSystem.IsWindowsVersionAtLeast(12, 0, 19222) || 
            OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2))
        {
            M2();
        }

        if(OperatingSystem.IsOSPlatformVersionAtLeast(""Linux"", 12) || 
            OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2))
        {
            [|M2()|];
        }
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfElseTestWithLogicalOr()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2) ||
           OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
        {
            M2();
        }
        else
        {
            [|M2()|];
        }
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfElseTestWithLogicalOrAndNegation()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        if (!(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2) ||
           OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11)))
        {
            [|M2()|];
        }
        else
        {
            M2();
        }
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfElseIfElseTestWithLogicalOr()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2) ||
           OperatingSystem.IsLinux())
        {
            [|M2()|]; //12
        }
        else if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
        {
            M2();
        }
        else
            [|M2()|];

        if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2) ||
           (OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(12, 0, 19222)))
        {
            [|M2()|];
        }
        else
        {
            [|M2()|];
        }

        if((OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(12, 0, 19222)) || 
            OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2))
        {
            [|M2()|]; //34
        }
        else
        {
            [|M2()|];
        }
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}
";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfElseTestWithLogicalOrAnd()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    void M1()
    {       
        if ((!OperatingSystem.IsWindows() || OperatingSystem.IsWindowsVersionAtLeast(10, 0, 1903)) &&
            (OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 2004)))
        {
            M2(); 
        }
        else
        {
            [|M2()|]; // This call site is reachable on all platforms. 'Test.M2()' is supported on: 'windows' from version 10.0.1903 to 10.0.2004.
        }
    }

    [UnsupportedOSPlatform(""windows"")]
    [SupportedOSPlatform(""windows10.0.1903"")]
    [UnsupportedOSPlatform(""windows10.0.2004"")]
    void M2() { }
}";
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);

            var vbSource = @"
Imports System.Runtime.Versioning
Imports System

Class Test
    Private Sub M1()
        If (Not OperatingSystem.IsWindows() OrElse OperatingSystem.IsWindowsVersionAtLeast(10, 0, 1903)) AndAlso (OperatingSystem.IsWindows() AndAlso Not OperatingSystem.IsWindowsVersionAtLeast(10, 0, 2004)) Then
            M2()
        Else
            [|M2()|]
        End If
    End Sub

    <UnsupportedOSPlatform(""Windows"")>
    <SupportedOSPlatform(""Windows10.0.1903"")>
    <UnsupportedOSPlatform(""Windows10.0.2004"")>
    Private Sub M2()
    End Sub
End Class";
            await VerifyAnalyzerAsyncVb(vbSource, s_msBuildPlatforms);
        }

        [Fact]
        public async Task GuardedWith_ControlFlowAndMultipleChecks()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    void M1()
    {
        if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 8))
        {
            [|M2()|];

            if (OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(12, 0, 19222))
            {
                [|M2()|];
            }
            else if (!OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2, 1))
            {
                [|M2()|];
            } 
            else
            {
                M2();
            }

            [|M2()|];
        }
        else
        {
            [|M2()|];
        }

        [|M2()|];
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    void M2() { }
}";

            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedWith_DebugAssertAnalysisTest()
        {
            var source = @"
using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
        M3();

        // Should still warn for Windows10.1.2.3 
        [|M2()|];

        Debug.Assert(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2));
        M2();
        M3();
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    void M2() { }

    [SupportedOSPlatform(""Windows"")]
    void M3() { }
}";
            await VerifyAnalyzerAsyncCs(source);

            var vbSource = @"
Imports System.Diagnostics
Imports System.Runtime.Versioning
Imports System

Class Test
    Private Sub M1()
        [|M2()|]
        Debug.Assert(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2))
        M2()
    End Sub

    <SupportedOSPlatform(""Windows10.1.2.3"")>
    Private Sub M2()
    End Sub
End Class";
            await VerifyAnalyzerAsyncVb(vbSource);
        }

        [Fact]
        public async Task GuardedWith_ResultSavedInLocal()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    void M1()
    {
        var x1 = OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11);
        var x2 = OperatingSystem.IsOSPlatformVersionAtLeast(""Linux"", 1);

        if (x1)
        {
            M2();
        }

        if (x1 || x2)
        {
            [|M2()|];
        }

        if (x2)
            [|M2()|];
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    void M2() { }
}";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task GuardedWith_VersionSavedInLocal()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    void M1()
    {
        var v11 = 11;
        if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", v11))
        {
            M2();
        }
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    void M2() { }
}";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task PlatformSavedInLocal_NotYetSupported() // TODO do we want to support it?
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    void M1()
    {
        var platform = ""Windows"";
        if (OperatingSystem.IsOSPlatformVersionAtLeast(platform, 11))
        {
            [|M2()|];
        }
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    void M2() { }
}";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task UnrelatedConditionCheckDoesNotInvalidateState()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    void M1(bool flag1, bool flag2)
    {
        [|M2()|];

        if (OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11))
        {
            M2();

            if (flag1 || flag2)
            {
                M2();
            }
            else
            {
                M2();
            }
            
            M2();
        }

        if (flag1 || flag2)
        {
            [|M2()|];
        }
        else
        {
            [|M2()|];
        }
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    void M2() { }
}";
            await VerifyAnalyzerAsyncCs(source);

            var vbSource = @"
Imports System.Runtime.Versioning
Imports System

Class Test
    Private Sub M1(ByVal flag1 As Boolean, ByVal flag2 As Boolean)
        [|M2()|]

        If OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 11) Then
            M2()

            If flag1 OrElse flag2 Then
                M2()
            Else
                M2()
            End If

            M2()
        End If

        If flag1 OrElse flag2 Then
            [|M2()|]
        Else
            [|M2()|]
        End If
    End Sub

    <SupportedOSPlatform(""Windows10.1.2.3"")>
    Private Sub M2()
    End Sub
End Class";
            await VerifyAnalyzerAsyncVb(vbSource);
        }

        [Fact]
        public async Task InterproceduralAnalysisTest()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    void M1()
    {
        [|M2()|];

        if (IsWindows11OrLater())
        {
            M2();    
        }
        else
        {
            [|M2()|]; 
        }
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    void M2() { }

    bool IsWindows11OrLater()
    {
        return OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"",10,2,3,4);
    }
}";

            await VerifyAnalyzerAsyncCs(source, "dotnet_code_quality.interprocedural_analysis_kind = ContextSensitive\nbuild_property.TargetFramework = net5");
        }

        [Fact, WorkItem(4282, "https://github.com/dotnet/roslyn-analyzers/issues/4282")]
        public async Task LambdaPassedAsArgumentOrNotInvokedWithinContextWouldNotAnalyzed_WithoutInterproceduralAnalysis()
        {
            var source = @"
using System;
using System.Diagnostics;
using System.Runtime.Versioning;

class Test
{
    public delegate void VoidCallback();
 
    public void DelegateAsArgument(VoidCallback callback) 
    {
        callback(); // even call back invoked here could not guarantee it called from guarded context
    }

    [SupportedOSPlatform(""windows"")]
    private void WindowsOnly() { }

    public void GuardedCalls()
    {
        if (OperatingSystem.IsWindows())
        {
            Action a = () =>
            {
                [|WindowsOnly()|]; // Warns, not invoked
            };

            Func<string> greetings = () =>
            {
                WindowsOnly(); // Not warn, invoked below
                return  ""Hi"";
            };
            greetings();

            DelegateAsArgument(() => [|WindowsOnly()|]); // Warns, couldn'd analyze
        }
    }

    public void AssertedCalls()
    {
        Debug.Assert(OperatingSystem.IsWindows());

        Action a = () =>
        {
            WindowsOnly(); // Not warn, invoked below
        };
        a();
            
        Func<string> greetings = () =>
        {
            [|WindowsOnly()|]; // Warns, not invoked
            return ""Hi"";
        };

        DelegateAsArgument(() => [|WindowsOnly()|]); // Warns, couldn'd analyze
    }
}";

            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact, WorkItem(4282, "https://github.com/dotnet/roslyn-analyzers/issues/4282")]
        public async Task LambdaPassedAsArgumentOrNotInvokedWithinContextWouldAnalyzed_WithInterproceduralAnalysis()
        {
            var source = @"
using System;
using System.Diagnostics;
using System.Runtime.Versioning;

class Test
{
    public delegate void VoidCallback();
 
    public void DelegateAsArgument(VoidCallback callback) 
    {
        callback(); // interprocedural analysis would help keep track of context
    }

    [SupportedOSPlatform(""windows"")]
    private void WindowsOnly() { }

    public void GuardedCalls()
    {
        if (OperatingSystem.IsWindows())
        {
            Action a = () =>
            {
                WindowsOnly(); // Not warn, interprocedural analysis enabled
            };

            Func<string> greetings = () =>
            {
                WindowsOnly(); // Same
                return  ""Hi"";
            };

            DelegateAsArgument(() => WindowsOnly()); // Same
        }
    }

    public void AssertedCalls()
    {
        Debug.Assert(OperatingSystem.IsWindows());

        Action a = () =>
        {
            WindowsOnly(); // Not warn, interprocedural analysis enabled
        };
            
        Func<string> greetings = () =>
        {
            WindowsOnly();
            return ""Hi"";
        };

        DelegateAsArgument(() => WindowsOnly());
    }
}";

            await VerifyAnalyzerAsyncCs(source, "dotnet_code_quality.interprocedural_analysis_kind = ContextSensitive");
        }

        [Fact, WorkItem(4182, "https://github.com/dotnet/roslyn-analyzers/issues/4182")]
        public async Task LoopWithinGuardCheck()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Repro
{
    public static partial class ProcessExtensions
    {
        public static int? GetParentProcessId(this Process process)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                foreach (var entry in GetProcesses())
                {
                }
            }
            else
            {
                string line = """";
                while (true)
                {
                    if (line.StartsWith("""", StringComparison.Ordinal))
                    {
                        if (true)
                        {
                        }
                    }
                }
            }

            return null;
        }

        [SupportedOSPlatform(""windows"")]
        public static IEnumerable<int> GetProcesses() => throw null;
    }
}";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact, WorkItem(4372, "https://github.com/dotnet/roslyn-analyzers/issues/4372")]
        public async Task LoopWithinGuardCheck_02()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Repro
{
    public class C
    {
        public static void M()
        {
            for (var i = 0; i < 2; i++)
            {
                if (OperatingSystem.IsWindows())
                {
                    WindowsSupported();
                }
            }
        }

        [SupportedOSPlatform(""windows"")]
        public static void WindowsSupported() { }
    }
}";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact, WorkItem(4372, "https://github.com/dotnet/roslyn-analyzers/issues/4372")]
        public async Task LoopWithinGuardCheck_03()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Repro
{
    public class C
    {
        public static void M()
        {
            if (OperatingSystem.IsWindows())
            {
                for (var i = 0; i < 2; i++)
                {
                    WindowsSupported();
                }
            }
        }

        [SupportedOSPlatform(""windows"")]
        public static void WindowsSupported() { }
    }
}";
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact(Skip = "TODO: Analysis value not returned, needs to be fixed")]
        public async Task InterproceduralAnalysisTest_LogicalOr()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    void M1()
    {
        [|M2()|];

        if (IsWindows11OrLater())
        {
            M2();    
        }

        [|M2()|]; 
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    void M2()
    {
    }

    bool IsWindows11OrLater()
    {
        return OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"",10,2,3,4) ||
            OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"",11);
    }
}";

            await VerifyAnalyzerAsyncCs(source, "dotnet_code_quality.interprocedural_analysis_kind = ContextSensitive");
        }

        private readonly string TargetTypesForTest = @"
namespace PlatformCompatDemo.SupportedUnupported
{
    public class TypeWithoutAttributes
    {
        [UnsupportedOSPlatform(""windows"")]
        public void FunctionUnsupportedOnWindows() { }

        [UnsupportedOSPlatform(""browser"")]
        public void FunctionUnsupportedOnBrowser() { }

        [UnsupportedOSPlatform(""windows10.0"")]
        public void FunctionUnsupportedOnWindows10() { }

        [UnsupportedOSPlatform(""windows""), UnsupportedOSPlatform(""browser"")]
        public void FunctionUnsupportedOnWindowsAndBrowser() { }

        [UnsupportedOSPlatform(""windows10.0""), UnsupportedOSPlatform(""browser"")]
        public void FunctionUnsupportedOnWindows10AndBrowser() { }

        [UnsupportedOSPlatform(""windows""), SupportedOSPlatform(""windows11.0"")]
        public void FunctionUnsupportedOnWindowsSupportedOnWindows11() { }

        [UnsupportedOSPlatform(""windows""), SupportedOSPlatform(""windows11.0""), UnsupportedOSPlatform(""windows12.0"")]
        public void FunctionUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12() { }

        [UnsupportedOSPlatform(""windows""), SupportedOSPlatform(""windows11.0""), UnsupportedOSPlatform(""windows12.0""), SupportedOSPlatform(""windows13.0"")]
        public void FunctionUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12SupportedOnWindows13() { }

        [SupportedOSPlatform(""windows"")]
        public void FunctionSupportedOnWindows() { }

        [SupportedOSPlatform(""windows10.0"")]
        public void FunctionSupportedOnWindows10() { }

        [SupportedOSPlatform(""browser"")]
        public void FunctionSupportedOnBrowser() { }

        [SupportedOSPlatform(""windows""), SupportedOSPlatform(""browser"")]
        public void FunctionSupportedOnWindowsAndBrowser() { }

        [SupportedOSPlatform(""windows10.0""), SupportedOSPlatform(""browser"")]
        public void FunctionSupportedOnWindows10AndBrowser() { }
    }

    [UnsupportedOSPlatform(""windows"")]
    public class TypeUnsupportedOnWindows {
        [UnsupportedOSPlatform(""browser"")] // more restrictive should be OK
        public void FunctionUnsupportedOnBrowser() { }

        [UnsupportedOSPlatform(""windows11.0"")]
        public void FunctionUnsupportedOnWindows11() { }

        [UnsupportedOSPlatform(""windows11.0""), UnsupportedOSPlatform(""browser"")]
        public void FunctionUnsupportedOnWindows11AndBrowser() { }

        [SupportedOSPlatform(""windows11.0"")]
        public void FunctionSupportedOnWindows11() { }

        [SupportedOSPlatform(""windows11.0""), UnsupportedOSPlatform(""windows12.0"")]
        public void FunctionSupportedOnWindows11UnsupportedOnWindows12() { }

        [SupportedOSPlatform(""windows11.0""), UnsupportedOSPlatform(""windows12.0""), SupportedOSPlatform(""windows13.0"")]
        public void FunctionSupportedOnWindows11UnsupportedOnWindows12SupportedOnWindows13() { }
    }

    [UnsupportedOSPlatform(""browser"")]
    public class TypeUnsupportedOnBrowser
    {
        [UnsupportedOSPlatform(""windows"")] // more restrictive should be OK
        public void FunctionUnsupportedOnWindows() { }

        [UnsupportedOSPlatform(""windows10.0"")] // more restrictive should be OK
        public void FunctionUnsupportedOnWindows10() { }
        
        [SupportedOSPlatform(""browser"")]
        public void FunctionSupportedOnBrowser() { }
    }

    [UnsupportedOSPlatform(""windows10.0"")]
    public class TypeUnsupportedOnWindows10
    {
        [UnsupportedOSPlatform(""browser"")] // more restrictive should be OK
        public void FunctionUnsupportedOnBrowser() { }

        [UnsupportedOSPlatform(""windows11.0"")]
        public void FunctionUnsupportedOnWindows11() { }

        [UnsupportedOSPlatform(""windows11.0""), UnsupportedOSPlatform(""browser"")]
        public void FunctionUnsupportedOnWindows11AndBrowser() { }
    }

    [UnsupportedOSPlatform(""windows""), UnsupportedOSPlatform(""browser"")]
    public class TypeUnsupportedOnWindowsAndBrowser
    {
        [UnsupportedOSPlatform(""windows11.0"")]
        public void FunctionUnsupportedOnWindows11() { }
    }

    [UnsupportedOSPlatform(""windows10.0""), UnsupportedOSPlatform(""browser"")]
    public class TypeUnsupportedOnWindows10AndBrowser
    {
        [UnsupportedOSPlatform(""windows11.0"")]
        public void FunctionUnsupportedOnWindows11() { }
    }

    [UnsupportedOSPlatform(""windows""), SupportedOSPlatform(""windows11.0"")]
    public class TypeUnsupportedOnWindowsSupportedOnWindows11
    {
        [UnsupportedOSPlatform(""windows12.0"")]
        public void FunctionUnsupportedOnWindows12() { }

        [UnsupportedOSPlatform(""windows12.0""), SupportedOSPlatform(""windows13.0"")]
        public void FunctionUnsupportedOnWindows12SupportedOnWindows13() { }
    }

    [UnsupportedOSPlatform(""windows""), SupportedOSPlatform(""windows11.0""), UnsupportedOSPlatform(""windows12.0"")]
    public class TypeUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12
    {
        [SupportedOSPlatform(""windows13.0"")]
        public void FunctionSupportedOnWindows13() { }
    }
    [SupportedOSPlatform(""windows"")]
    public class TypeSupportedOnWindows {
        [SupportedOSPlatform(""browser"")]
        public void FunctionSupportedOnBrowser() { }

        [SupportedOSPlatform(""windows11.0"")] // more restrictive should be OK
        public void FunctionSupportedOnWindows11() { }

        [SupportedOSPlatform(""windows11.0""), SupportedOSPlatform(""browser"")]
        public void FunctionSupportedOnWindows11AndBrowser() { }
    }
    [SupportedOSPlatform(""browser"")]
    public class TypeSupportedOnBrowser
    {
        [SupportedOSPlatform(""windows"")]
        public void FunctionSupportedOnWindows() { }

        [SupportedOSPlatform(""windows11.0"")]
        public void FunctionSupportedOnWindows11() { }
    }

    [SupportedOSPlatform(""windows10.0"")]
    public class TypeSupportedOnWindows10
    {
        [SupportedOSPlatform(""windows"")] // less restrictive should be OK
        public void FunctionSupportedOnWindows() { }

        [SupportedOSPlatform(""browser"")]
        public void FunctionSupportedOnBrowser() { }

        [SupportedOSPlatform(""windows11.0"")] // more restrictive should be OK
        public void FunctionSupportedOnWindows11() { }

        [SupportedOSPlatform(""windows11.0""), SupportedOSPlatform(""browser"")]
        public void FunctionSupportedOnWindows11AndBrowser() { }
    }


    [SupportedOSPlatform(""windows""), SupportedOSPlatform(""browser"")]
    public class TypeSupportedOnWindowsAndBrowser
    {
        [SupportedOSPlatform(""windows11.0"")] // more restrictive should be OK
        public void FunctionSupportedOnWindows11() { }
    }

    [SupportedOSPlatform(""windows10.0""), SupportedOSPlatform(""browser"")]
    public class TypeSupportedOnWindows10AndBrowser
    {
        [SupportedOSPlatform(""windows11.0"")] // more restrictive should be OK
        public void TypeSupportedOnWindows10AndBrowser_FunctionSupportedOnWindows11() { }
    }
}";
    }
}
