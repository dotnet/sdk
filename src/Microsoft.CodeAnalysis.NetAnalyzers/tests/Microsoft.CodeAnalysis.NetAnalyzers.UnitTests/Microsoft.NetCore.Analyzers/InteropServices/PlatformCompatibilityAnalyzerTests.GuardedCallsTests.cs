// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
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
        public async Task GuardMethodWithNamedArgumentsTestAsync(string arguments)
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

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task MethodsWithOsDependentTypeParameterGuardedAsync()
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
            await VerifyAnalyzerCSAsync(csSource, s_msBuildPlatforms);
        }

        [Fact]
        public async Task PlatformDependentMethodsAndTypeParametersGuardedAsync()
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
            {|#0:WindowsOnlyMethod<BrowserOnlyType>()|};  // should flag for WindowsOnlyMethod method and BrowserOnlyType parameter
            {|#1:GenericMethod<WindowsOnlyType, BrowserOnlyType>()|}; // should flag for WindowsOnlyType and BrowserOnlyType parameters
        }
    }
}
";
            await VerifyAnalyzerCSAsync(csSource, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(0).WithArguments("BrowserOnlyType", "'browser'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(0).WithArguments("Test.WindowsOnlyMethod<BrowserOnlyType>()", "'windows'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(1).WithArguments("WindowsOnlyType", "'windows'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(1).WithArguments("BrowserOnlyType", "'browser'"));
        }

        [Fact]
        public async Task SupportedUnsupportedRange_GuardedWithOrAsync()
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

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact, WorkItem(4932, "https://github.com/dotnet/roslyn-analyzers/issues/4932")]
        public async Task GuardMethodWith3VersionPartsEquavalentTo4PartsWithLeading0Async()
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

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardMethodWith1VersionPartsEquavalentTo2PartsWithLeading0Async()
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

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardMethodWith2VersionPartsEquavalentTo3PartsWithLeading0Async()
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

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardsAroundSupported_SimpleIfElseAsync()
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

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact, WorkItem(6833, "https://github.com/dotnet/roslyn-analyzers/issues/6833")]
        public async Task GuardsAroundSupported_InsideTryBlockAsync()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

namespace PlatformCompatDemo.Bugs.GuardsAroundSupported
{
    class Caller
    {
        public static void TestWithGuardMethods(bool f1, bool f2)
        {
            try
            {
                [|Target.SupportedOnWindows()|];
            }
            catch (Exception e1) when (f1)
            {
            }
            catch (Exception e2) when (f2)
            {
            }
            finally
            {
                if (OperatingSystem.IsWindows())
                    Target.SupportedOnWindowsAndBrowser();
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

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task SupportedOnOsx_GuardedWithIsMacOSAsync()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    public void Api_Usage()
    {
        if (OperatingSystem.IsWindows() ||
            OperatingSystem.IsLinux() ||
            OperatingSystem.IsMacOS())
        {
            Api();
            Api2();
        }

        [|Api()|]; // This call site is reachable on all platforms. 'Test.Api()' is only supported on: 'macOS/OSX', 'Linux', 'windows'.
        [|Api2()|]; // This call site is reachable on all platforms. 'Test.Api2()' is only supported on: 'macOS/OSX', 'Linux', 'windows'.
    }

    [SupportedOSPlatform(""macos"")]
    [SupportedOSPlatform(""windows"")]
    [SupportedOSPlatform(""Linux"")]
    void Api() { }

    [SupportedOSPlatform(""windows"")]
    [SupportedOSPlatform(""Osx"")]
    [SupportedOSPlatform(""Linux"")]
    void Api2() { }
}";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task UnsupportedOnOsx_GuardedWithIsMacOSAsync()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    public void Api_Usage()
    {
        if (!OperatingSystem.IsWindows() &&
            !OperatingSystem.IsLinux() &&
            !OperatingSystem.IsMacOS())
        {
            Api();
        }

        [|Api()|]; // This call site is reachable on all platforms. 'Test.Api()' is unsupported on: 'macOS/OSX', 'windows'.
    }

    [UnsupportedOSPlatform(""windows"")]
    [UnsupportedOSPlatform(""Linux"")]
    [UnsupportedOSPlatform(""Osx"")]
    void Api() { }
}";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task SupportedOnOsxVersioned_GuardedWithIsMacOSVersionedAsync()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    public void Api_Usage()
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10) ||
            OperatingSystem.IsLinux() ||
            OperatingSystem.IsMacOSVersionAtLeast(11))
        {
            Api();
        }

        [|Api()|]; // This call site is reachable on all platforms. 'Test.Api2()' is only supported on: 'macOS/OSX' 10.1 and later, 'windows' 10.0 and later, 'Linux'.
    }

    [SupportedOSPlatform(""windows10.0"")]
    [SupportedOSPlatform(""Linux"")]
    [SupportedOSPlatform(""Osx10.1"")]
    void Api() { }
}";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task SupportedUnsupportedOnOsx_GuardedWithIsMacOS_MessageParameterTestAsync()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

class Test
{
    public void Api_Usage()
    {
        if (OperatingSystem.IsMacOS())
        {
            MacOsApi();
            OsxApi();
            {|#0:UnsupportedOsxApi()|};
        }
        else
        {
            {|#1:MacOsApi()|}; // This call site is reachable on all platforms. 'Test.Api()' is only supported on: 'macOS/OSX', 'Linux', 'windows'.
            {|#2:OsxApi()|}; // This call site is reachable on all platforms. 'Test.Api2()' is only supported on: 'macOS/OSX', 'Linux', 'windows'.
            UnsupportedOsxApi();
        }
    }

    [SupportedOSPlatform(""macos"")]
    void MacOsApi() { }

    [SupportedOSPlatform(""Osx"")]
    void OsxApi() { }

    [UnsupportedOSPlatform(""Osx"")]
    void UnsupportedOsxApi() { }
}";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable)
                    .WithLocation(0).WithArguments("Test.UnsupportedOsxApi()", "'macOS/OSX'", "'macOS/OSX'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms)
                    .WithLocation(1).WithArguments("Test.MacOsApi()", "'macOS/OSX'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms)
                    .WithLocation(2).WithArguments("Test.OsxApi()", "'macOS/OSX'"));
        }

        [Fact]
        public async Task GuardsAroundSupportedAsync()
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

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task MoreGuardsAroundSupportedAsync()
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

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task MoreGuardsAroundUnSupportedAsync()
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

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task GuardsAroundUnsupportedAsync()
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

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task SupportedUnsupportedRange_GuardedWithAndAsync()
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

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task Unsupported_GuardedWith_IsOsNameMethodsAsync()
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

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact, WorkItem(4190, "https://github.com/dotnet/roslyn-analyzers/issues/4190")]
        public async Task Unsupported_GuardedWith_DebugAssert_IsOsNameMethodsAsync()
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
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
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
        public async Task GuardedWith_IsOsNameVersionAtLeast_SimpleIfElseAsync(string osName, string isOsMethod, string version, bool versionMatch)
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

            await VerifyAnalyzerCSAsync(source);
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
        public async Task GuardedWith_IsOsNameMethods_SimpleIfElseAsync(string osName, string isOsMethod)
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

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact, WorkItem(5938, "https://github.com/dotnet/roslyn-analyzers/issues/5938")]
        public async Task Guarded_TwoConditionalsAndReturns_WithCallSiteAttribute()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    [SupportedOSPlatform(""ios"")]
    public void M1()
    {
        [|M2()|];
        if(OperatingSystem.IsIOS() && !OperatingSystem.IsIOSVersionAtLeast(13, 0))
        {
            M2();
            return;
        }
        M3(); // should not warn as ios 13.0 or below case returns with above condition
    }

    [SupportedOSPlatform(""ios"")]
    [UnsupportedOSPlatform(""ios13.0"")]
    [SupportedOSPlatform(""tvos"")]
    [UnsupportedOSPlatform(""tvos13.0"")]
    public void M2() { }

    [SupportedOSPlatform(""ios13.0"")]
    [SupportedOSPlatform(""tvos13.0"")]
    public void M3() { }
}
";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedWith_OperatingSystem_IsOSPlatform_SimpleIfElseAsync()
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

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact, WorkItem(5963, "https://github.com/dotnet/roslyn-analyzers/pull/5963")]
        public async Task IosGuardAttributeWithinMacCatalystTargetedAssembly()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform(""MacCatalyst13.1"")]

public class Test
{
    [SupportedOSPlatformGuard(""ios14.0"")]
	internal static bool IsiOS14OrNewer => true;

    [SupportedOSPlatform(""ios13.4"")]
    public static void iOS13Method() { }

    static void M1()
    {
        [|iOS13Method()|]; // This call site is reachable on: 'MacCatalyst' 13.1 and later. 'Test.iOS13Method()' is only supported on: 'MacCatalyst' 13.4 and later.
        if (IsiOS14OrNewer)
            iOS13Method(); // Should not warn
            
    }
}";
            await VerifyAnalyzerCSAsync(csSource);
        }

        [Fact]
        public async Task CallGuardFromPlatformSpecificAssembly()
        {
            string csDependencyCode = @"
public class Library
{
    static bool s_isWindowsOrLinux = false;

    [System.Runtime.Versioning.SupportedOSPlatformGuard(""windows"")]
    [System.Runtime.Versioning.SupportedOSPlatformGuard(""linux"")]
    public static bool IsSupported => s_isWindowsOrLinux;

    [System.Runtime.Versioning.UnsupportedOSPlatformGuard(""windows"")]
    [System.Runtime.Versioning.UnsupportedOSPlatformGuard(""linux"")]
    public static bool IsNotSupported => false;

    public static void AMethod() { }
}";
            csDependencyCode = @$"[assembly: System.Runtime.Versioning.SupportedOSPlatform(""windows"")]
                                  [assembly: System.Runtime.Versioning.SupportedOSPlatform(""linux"")]
                                  {csDependencyCode}";

            string csCurrentAssemblyCode = @"
using System;

public class Program
{
    public void ProgramMethod()
    {
        [|Library.AMethod()|]; // Not guarded, warns
        if (Library.IsSupported)
        {
             Library.AMethod();
        }

        if (Library.IsNotSupported)
        {
             [|Library.AMethod()|]; // warn because guarded by unsupported
        }
        else
        {
             Library.AMethod(); // guarded
        }
    }
}";
            var test = SetupDependencyAndTestCSWithOneSourceFile(csCurrentAssemblyCode, csDependencyCode);
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", $@"root = true

[*]
build_property.TargetFramework = net5
build_property.TargetFrameworkIdentifier = .NETCoreApp
build_property.TargetFrameworkVersion = v5.0
"));
            await test.RunAsync();
        }

        private static VerifyCS.Test SetupDependencyAndTestCSWithOneSourceFile(string csInput, string csDependencyCode)
        {
            return new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp10,
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
                TestState =
                {
                    Sources =
                    {
                        csInput
                    },
                    AdditionalProjects =
                    {
                        ["PreviewAssembly"] =
                        {
                            Sources =
                            {
                                ("/PreviewAssembly/AssemblyInfo.cs", csDependencyCode)
                            },
                        },
                    },
                    AdditionalProjectReferences =
                    {
                        "PreviewAssembly",
                    },
                },
            };
        }

        [Fact]
        public async Task GuardedWith_RuntimeInformation_IsOSPlatform_SimpleIfElseAsync()
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

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedWith_RuntimeInformation_IsOSPlatform_OSX_GuardsMacOS()
        {
            var source = @"
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            SupportsMacOS();
            [|UnsupportsMacOS()|];
            SupportsOSX();
            [|UnsupportsOSX()|];
        }
        else
        {
            [|SupportsMacOS()|];
            UnsupportsMacOS();
            [|SupportsOSX()|];
            UnsupportsOSX();
        }
    }

    [SupportedOSPlatform(""macos"")]
    void SupportsMacOS() { }

    [UnsupportedOSPlatform(""MacOS"")]
    void UnsupportsMacOS() { }

    [SupportedOSPlatform(""OSX"")]
    void SupportsOSX() { }

    [UnsupportedOSPlatform(""osx"")]
    void UnsupportsOSX() { }
}";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact, WorkItem(4119, "https://github.com/dotnet/roslyn-analyzers/issues/4119")]
        public async Task GuardedWith_RuntimeInformation_IsOSPlatform_OSPlatformCreate_SimpleIfElseAsync()
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

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact, WorkItem(4119, "https://github.com/dotnet/roslyn-analyzers/issues/4119")]
        public async Task GuardedWith_RuntimeInformation_IsOSPlatform_OSPlatformCreate_ValueCachedInLocal_SimpleIfElseAsync()
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

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact, WorkItem(4119, "https://github.com/dotnet/roslyn-analyzers/issues/4119")]
        public async Task GuardedWith_RuntimeInformation_IsOSPlatform_OSPlatformCreate_MultipleValuesCachedWithConditionalLogicAsync()
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

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedCalled_SimpleIfElse_VersionNotMatch_WarnsAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task ReintroducingApiSupport_Guarded_NotWarnAsync()
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

            await VerifyAnalyzerCSAsync(source,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsReachable).WithLocation(0).
                WithArguments("Some.WindowsSpecificApi()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"),
                GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows")));
        }

        [Fact]
        public async Task GuardedCalled_SimpleIf_NotWarnsAsync()
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
            await VerifyAnalyzerCSAsync(source);

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
            await VerifyAnalyzerVBAsync(vbSource);
        }

        [Fact]
        public async Task GuardedCall_MultipleSimpleIfTestsAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedWith_IsOSPlatformVersionAtLeast_SimpleIfElseAsync()
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

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedWith_AlternativeOf_IsOSPlatformEarlierThanAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedWith_Unsupported_SimpleIfElseAsync()
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
            M2();
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
            await VerifyAnalyzerCSAsync(source);

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
            M2()
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
            await VerifyAnalyzerVBAsync(vbSource);
        }

        [Fact]
        public async Task OsDependentEnumValue_GuardedCall_SimpleIfElseAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task OsDependentProperty_GuardedCall_SimpleIfElseAsync()
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
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact, WorkItem(4105, "https://github.com/dotnet/roslyn-analyzers/issues/4105")]
        public async Task OsDependentPropertyWithInitializer_NoDiagnosticAsync()
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
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact, WorkItem(4105, "https://github.com/dotnet/roslyn-analyzers/issues/4105")]
        public async Task OsDependentPropertyWithInitializer_DiagnosticAsync()
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
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact, WorkItem(4105, "https://github.com/dotnet/roslyn-analyzers/issues/4105")]
        public async Task OsDependentFieldWithInitializer_NoDiagnosticAsync()
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
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact, WorkItem(4105, "https://github.com/dotnet/roslyn-analyzers/issues/4105")]
        public async Task OsDependentFieldWithInitializer_DiagnosticAsync()
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
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task OsDependentConstructorOfClass_GuardedCall_SimpleIfElseAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task ConstructorAndMethodOfOsDependentClass_GuardedCall_SimpleIfElseAsync()
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
            await VerifyAnalyzerCSAsync(source);

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
            await VerifyAnalyzerVBAsync(vbSource);
        }

        [Fact]
        public async Task LocalFunctionCallsOsDependentMember_GuardedCall_SimpleIfElseAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LocalFunctionCallsPlatformDependentMember_InvokedFromDifferentContextAsync()
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
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task LocalFunctionCallsPlatformDependentMember_InvokedFromNotGuardedDifferentContextAsync()
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
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task LocalFunctionCallsPlatformDependentMember_InvokedFromGuardedDifferentContextAsync()
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
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task LocalFunctionEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElseAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LocalFunctionEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_02Async()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LocalFunctionEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_03Async()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LocalFunctionEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_04Async()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LocalFunctionMultipleCallsOsDependentMember_MixedGuardedCallsAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LocalFunctionMultipleCalls_DifferentOrder_OsDependentMember_MixedGuardedCallsAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LocalFunctionWithUnrelatedLocalFunctionCallsOsDependentMember_GuardedCallsAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LocalFunctionUnusedCallsOsDependentMember_GuardedCalls_SimpleIfElseAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LambdaCallsOsDependentMember_GuardedCall_SimpleIfElseAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact, WorkItem(4090, "https://github.com/dotnet/roslyn-analyzers/issues/4090")]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_DirectlyPassedAsArgumentAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElseAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_02Async()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_03Async()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_04Async()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_05Async()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_06Async()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_07Async()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMember_GuardedCalls_SimpleIfElse_08Async()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LambdaUnusedCallsOsDependentMember_GuardedCalls_SimpleIfElseAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LambdaMultipleCallsOsDependentMember_MixedGuardedCallsAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LambdaWithUnrelatedLambdaCallsOsDependentMember_GuardedCallsAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LambdaContaingLocalFunctionCallsOsDependentMember_GuardedCall_SimpleIfElseAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LocalFunctionContainingLambdaCallsOsDependentMember_GuardedCall_SimpleIfElseAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact, WorkItem(4209, "https://github.com/dotnet/roslyn-analyzers/issues/4209")]
        public async Task LambdaInvocationWithUnknownTarget_BeforeGuardedCallAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task OsDependentEventAccessed_GuardedCall_SimpleIfElseAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task OsDependentMethodAssignedToDelegate_GuardedCall_SimpleIfElseAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfElseIfElseTestAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfElseTestWithNegationAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfElseIfElseTestWithNegationAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfTestWithNegationAndReturnAsync()
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
            await VerifyAnalyzerCSAsync(source);

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
            await VerifyAnalyzerVBAsync(vbSource);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfTestWithLogicalAndAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfElseTestWithLogicalAndAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfTestWithLogicalOrAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfElseTestWithLogicalOrAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfElseTestWithLogicalOrAndNegationAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfElseIfElseTestWithLogicalOrAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedCall_SimpleIfElseTestWithLogicalOrAndAsync()
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
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);

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
            await VerifyAnalyzerVBAsync(vbSource, s_msBuildPlatforms);
        }

        [Fact]
        public async Task GuardedWith_ControlFlowAndMultipleChecksAsync()
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

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedWith_DebugAssertAnalysisTestAsync()
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
            await VerifyAnalyzerCSAsync(source);

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
            await VerifyAnalyzerVBAsync(vbSource);
        }

        [Fact]
        public async Task GuardedWith_DebugAssertWithMessage()
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
        Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), ""Only supported on windows"", ""Detailed message"");
        M3();
        [|M2()|];
        [|M4()|];

        Debug.Assert(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2), ""Only supported on windows"");
        M2();
        M3();

        Debug.Assert(OperatingSystem.IsLinux(), ""{0}  is only supported on windows"", ""Detailed message"", nameof(M4));
        M4();
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    void M2() { }

    [SupportedOSPlatform(""Windows"")]
    void M3() { }

    [SupportedOSPlatform(""linux"")]
    void M4() { }
}";
            await VerifyAnalyzerCSAsync(source);

            var vbSource = @"
Imports System
Imports System.Diagnostics
Imports System.Runtime.Versioning

Class Test
    Private Sub M1()
        Debug.Assert(OperatingSystem.IsWindows(), ""Only supported on windows"", ""Detailed message"")
        M3()
        [|M2()|]
        [|M4()|]

        Debug.Assert(OperatingSystem.IsOSPlatformVersionAtLeast(""Windows"", 10, 2), ""Only supported on windows"")
        M2()
        M3()

        Debug.Assert(OperatingSystem.IsLinux(), ""{0}  is only supported on windows"", ""Detailed message"", nameof(M4))
        M4()
    End Sub

    <SupportedOSPlatform(""Windows10.1.2.3"")>
    Private Sub M2()
    End Sub
    <SupportedOSPlatform(""Windows"")>
    Private Sub M3()
    End Sub
    <SupportedOSPlatform(""Linux"")>
    Private Sub M4()
    End Sub
End Class";
            await VerifyAnalyzerVBAsync(vbSource);
        }

        [Fact]
        public async Task GuardedWith_ResultSavedInLocalAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task GuardedWith_VersionSavedInLocalAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task PlatformSavedInLocal_NotYetSupportedAsync() // TODO do we want to support it?
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task UnrelatedConditionCheckDoesNotInvalidateStateAsync()
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
            await VerifyAnalyzerCSAsync(source);

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
            await VerifyAnalyzerVBAsync(vbSource);
        }

        [Fact]
        public async Task InterproceduralAnalysisTestAsync()
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

            await VerifyAnalyzerCSAsync(source, "dotnet_code_quality.interprocedural_analysis_kind = ContextSensitive\nbuild_property.TargetFramework = net5\nbuild_property.TargetFrameworkIdentifier = .NETCoreApp\nbuild_property.TargetFrameworkVersion = v5.0");
        }

        [Fact, WorkItem(4282, "https://github.com/dotnet/roslyn-analyzers/issues/4282")]
        public async Task LambdaPassedAsArgumentOrNotInvokedWithinContextWouldNotAnalyzed_WithoutInterproceduralAnalysisAsync()
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

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact, WorkItem(4282, "https://github.com/dotnet/roslyn-analyzers/issues/4282")]
        public async Task LambdaPassedAsArgumentOrNotInvokedWithinContextWouldAnalyzed_WithInterproceduralAnalysisAsync()
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

            await VerifyAnalyzerCSAsync(source, "dotnet_code_quality.interprocedural_analysis_kind = ContextSensitive");
        }

        [Fact, WorkItem(4182, "https://github.com/dotnet/roslyn-analyzers/issues/4182")]
        public async Task LoopWithinGuardCheckAsync()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact, WorkItem(4372, "https://github.com/dotnet/roslyn-analyzers/issues/4372")]
        public async Task LoopWithinGuardCheck_02Async()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact, WorkItem(4372, "https://github.com/dotnet/roslyn-analyzers/issues/4372")]
        public async Task LoopWithinGuardCheck_03Async()
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
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact(Skip = "TODO: Analysis value not returned, needs to be fixed")]
        public async Task InterproceduralAnalysisTest_LogicalOrAsync()
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

            await VerifyAnalyzerCSAsync(source, "dotnet_code_quality.interprocedural_analysis_kind = ContextSensitive");
        }

        [Fact, WorkItem(5963, "https://github.com/dotnet/roslyn-analyzers/pull/5963")]
        public async Task GuardCallingCachedValue_CallSiteHasAssemblyAttributeAsync()
        {
            var source = @"
using System;
using System.Diagnostics;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform(""ios10.0"")]
class Test
{
    static bool s_isiOS11OrNewer = false;

    [SupportedOSPlatformGuard(""ios11.0"")]
    private bool IsIos11Supported() => s_isiOS11OrNewer; // should not warn

    void M1()
    {
        [|SupportedOniOS11()|]; 

        if (IsIos11Supported())
        {
            SupportedOniOS11();    
        }
    }

    [SupportedOSPlatform(""ios11.0"")]
    void SupportedOniOS11() { }
}";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task GuardMembersWithSupportedGuardAttributesAsync()
        {
            var source = @"
using System;
using System.Diagnostics;
using System.Runtime.Versioning;

class Test
{
    [SupportedOSPlatformGuard(""linux"")]
    private bool IsLunuxSupported() => true;

    [SupportedOSPlatformGuard(""linux"")]
    [SupportedOSPlatformGuard(""Windows"")]
    internal bool LinuxAndWindowsSupported { get; set; }

    [SupportedOSPlatformGuard(""linux"")]
    [SupportedOSPlatformGuard(""macOS"")]
    [SupportedOSPlatformGuard(""Windows"")]
    private readonly bool _http3Enabled;

    [SupportedOSPlatformGuard(""linux"")]
    [SupportedOSPlatformGuard(""macOS"")]
    [SupportedOSPlatformGuard(""Windows"")]
    [SupportedOSPlatformGuard(""Android"")]
    private bool IsHttp3PlusAndroidEnabled() => false;

    [SupportedOSPlatformGuard(""linux"")]
    [SupportedOSPlatformGuard(""macOS"")]
    [SupportedOSPlatformGuard(""Windows"")]
    [UnsupportedOSPlatformGuard(""Android"")]
    private readonly bool _http3EnabledNotAndroid;

    void M1()
    {
        if (IsLunuxSupported()) // one of the support guarded, so no warning
        {
            SupportedOnWindowsLinuxOsx();    
            SupportedOnLinux();
        }

        if (_http3Enabled)
        {
            SupportedOnWindowsLinuxOsx();  
            [|SupportedOnLinux()|]; // only supported on linux but call site is reachable on 3, linux, windows, macos
        }
        else
        {
            {|#0:SupportedOnWindowsLinuxOsx()|}; // This call site is reachable on all platforms. 'Test.SupportedOnWindowsLinuxOsx()' is only supported on: 'Linux', 'macOS/OSX', 'windows'
            {|#1:SupportedOnLinux()|}; // This call site is reachable on all platforms. 'Test.SupportedOnLinux()' is only supported on: 'linux'.
        }

        if (IsHttp3PlusAndroidEnabled()) // Android is not in the support list
        {
            {|#2:SupportedOnWindowsLinuxOsx()|}; // This call site is reachable on: 'Android'. 'Test.SupportedOnWindowsLinuxOsx()' is only supported on: 'Linux', 'macOS/OSX', 'windows'.        
        }

        if (_http3EnabledNotAndroid)
        {
            SupportedOnWindowsLinuxOsx();  
            [|SupportedOnLinux()|];
        }
        
        [|SupportedOnWindowsLinuxOsx()|];
        Debug.Assert(LinuxAndWindowsSupported);
        SupportedOnWindowsLinuxOsx();    
        [|SupportedOnLinux()|];
    }

    [SupportedOSPlatform(""windows"")]
    [SupportedOSPlatform(""Linux"")]
    [SupportedOSPlatform(""OSX"")]
    void SupportedOnWindowsLinuxOsx() { }

    [SupportedOSPlatform(""linux"")]
    void SupportedOnLinux() { }
}";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(0).
                    WithArguments("Test.SupportedOnWindowsLinuxOsx()", Join("'Linux'", "'macOS/OSX'", "'windows'")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(1).
                    WithArguments("Test.SupportedOnLinux()", "'linux'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(2).
                    WithArguments("Test.SupportedOnWindowsLinuxOsx()", Join("'Linux'", "'macOS/OSX'", "'windows'"), "'Android'"));
        }

        [Fact]
        public async Task GuardMembersWithUnsupportedGuardAttributesAsync()
        {
            var source = @"
using System;
using System.Diagnostics;
using System.Runtime.Versioning;

class Test
{
    [UnsupportedOSPlatformGuard(""linux"")]
    private readonly bool _linuxNotSupported;

    [UnsupportedOSPlatformGuard(""linux"")]
    [UnsupportedOSPlatformGuard(""Windows"")]
    public bool LinuxAndWindowsNotSupported { get; }

    [UnsupportedOSPlatformGuard(""linux"")]
    [UnsupportedOSPlatformGuard(""ios"")]
    [UnsupportedOSPlatformGuard(""Windows"")]
    private bool IsLinuxWindowsIosNotSupported() => true;

    [UnsupportedOSPlatformGuard(""linux"")]
    [UnsupportedOSPlatformGuard(""ios"")]
    [UnsupportedOSPlatformGuard(""Windows"")]
    [UnsupportedOSPlatformGuard(""Android"")]
    private readonly bool _linuxWindowsIosAndroidNotSupported;

    void M1()
    {
        if (_linuxNotSupported)
        {
            UnsupportedOnLinux();
            [|UnsupportedOnLinuxWindowsIos()|]; // This call site is reachable on all platforms. 'Test.UnsupportedOnLinuxWindowsIos()' is unsupported on: 'windows', 'ios'.
        }

        if (LinuxAndWindowsNotSupported)
        {
            UnsupportedOnLinux();
            [|UnsupportedOnLinuxWindowsIos()|]; // This call site is reachable on all platforms. 'Test.UnsupportedOnLinuxWindowsIos()' is unsupported on: 'ios'.
        }

        if (_linuxWindowsIosAndroidNotSupported)
        {
            UnsupportedOnLinux();
            UnsupportedOnLinuxWindowsIos();
        }
        else
        {
            [|UnsupportedOnLinux()|]; // This call site is reachable on: 'Windows', 'linux', 'Android'. 'Test.UnsupportedOnLinux()' is unsupported on: 'Linux'.
            [|UnsupportedOnLinuxWindowsIos()|]; // This call site is reachable on: 'Android', 'Windows'. 'Test.UnsupportedOnLinuxWindowsIos()' is unsupported on: 'Linux', 'ios', 'windows'.
        }

        Debug.Assert(IsLinuxWindowsIosNotSupported());
        UnsupportedOnLinux();
        UnsupportedOnLinuxWindowsIos();
    }


    [UnsupportedOSPlatform(""Linux"")]
    void UnsupportedOnLinux() { }

    [UnsupportedOSPlatform(""windows"")]
    [UnsupportedOSPlatform(""Linux"")]
    [UnsupportedOSPlatform(""ios"")]
    void UnsupportedOnLinuxWindowsIos() { }
}";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task GuardMembersWithVersionedSupportedUnsupportedGuardAttributesAsync()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

class Test
{
    [SupportedOSPlatformGuard(""Windows10.0"")]
    private bool IsWindow10Supported() => true;

    [SupportedOSPlatformGuard(""linux4.8"")]
    [SupportedOSPlatformGuard(""Windows10.0"")]
    [SupportedOSPlatformGuard(""Osx14.1"")]
    private readonly bool _linuxAndWindows10MacOS14Supported;

    [UnsupportedOSPlatformGuard(""linux"")]
    [UnsupportedOSPlatformGuard(""ios9.0"")]
    [UnsupportedOSPlatformGuard(""Windows8.0"")]
    private bool LinuxWindows8Ios9NotSupported { get; }

    void M1()
    {
        if (IsWindow10Supported())
        {
            {|#0:UnsupportedOnLinuxWindows10Ios91()|}; // This call site is reachable on: 'Windows' 10.0 and later. 'Test.UnsupportedOnLinuxWindows10Ios91()' is unsupported on: 'windows' 10.0 and later.
            SupportedOnWindows10LinuxMacOS14();
            SupportedOnWindows8();
        }

        if (_linuxAndWindows10MacOS14Supported)
        {
            [|UnsupportedOnLinuxWindows10Ios91()|]; // This call site is reachable on: 'Windows' 10.0 and later. 'Test.UnsupportedOnLinuxWindows10Ios91()' is unsupported on: 'windows' 10.0 and later, 'Linux', 'ios' 9.1 and later.
            SupportedOnWindows10LinuxMacOS14();
            [|SupportedOnWindows8()|]; // This call site is reachable on: 'linux'. 'Test.SupportedOnWindows8()' is only supported on: 'windows' 8.0 and later.
        }

        if (LinuxWindows8Ios9NotSupported)
        {
            UnsupportedOnLinuxWindows10Ios91();
            {|#1:SupportedOnWindows10LinuxMacOS14()|}; // This call site is reachable on all platforms. 'Test.SupportedOnWindows10LinuxMacOS14()' is only supported on: 'linux' 4.8 and later, 'macOS/OSX' 14.0 and later, 'windows' 10.0 and later.
            {|#2:SupportedOnWindows8()|}; // This call site is reachable on all platforms. 'Test.SupportedOnWindows8()' is only supported on: 'windows' 8.0 and later.
        }
    }

    [UnsupportedOSPlatform(""windows10.0"")]
    [UnsupportedOSPlatform(""Linux"")]
    [UnsupportedOSPlatform(""ios9.1"")]
    void UnsupportedOnLinuxWindows10Ios91() { }

    [SupportedOSPlatform(""windows10.0"")]
    [SupportedOSPlatform(""linux4.8"")]
    [SupportedOSPlatform(""macOS14.0"")]
    void SupportedOnWindows10LinuxMacOS14() { }

    [SupportedOSPlatform(""windows8.0"")]
    void SupportedOnWindows8() { }
}";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0).WithArguments("Test.UnsupportedOnLinuxWindows10Ios91()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"),
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "Windows", "10.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(1).WithArguments("Test.SupportedOnWindows10LinuxMacOS14()",
                    Join(GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "linux", "4.8"),
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "macOS/OSX", "14.0"),
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"))),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(2).WithArguments("Test.SupportedOnWindows8()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "8.0")));
        }

        [Fact]
        public async Task GuardMembersWithSupportedUnsupportedVersionRangeGuardAttributesAsync()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

class Test
{
    [SupportedOSPlatformGuard(""Windows"")]
    [UnsupportedOSPlatformGuard(""Windows10.0"")]
    public bool SupportedUntilWindow10 => true;

    [SupportedOSPlatformGuard(""linux"")]
    [SupportedOSPlatformGuard(""Windows"")]
    [UnsupportedOSPlatformGuard(""Windows10.0"")]
    private bool IsSupportedUntilWindow10AndLinux() => false;

    [UnsupportedOSPlatformGuard(""ios"")]
    [SupportedOSPlatformGuard(""ios14.0"")]
    [UnsupportedOSPlatformGuard(""ios18.0"")]
    [UnsupportedOSPlatformGuard(""Windows8.0"")]
    private readonly bool _windows8IosNotSupportedSupportedIos14_18;

    void M1()
    {
        if (SupportedUntilWindow10)
        {
            {|#0:UnsupportedOnWindows8IosSupportsIos14_19()|}; // This call site is reachable on: 'Windows' 10.0 and before. 'Test.UnsupportedOnWindows8IosSupportsIos14_19()' is unsupported on: 'Windows' 8.0 and later.
            SupportedOnWindowsUntil10AndLinux();
            SupportedOnWindowsUntil10();
        }

        if (IsSupportedUntilWindow10AndLinux())
        {
            [|UnsupportedOnWindows8IosSupportsIos14_19()|]; // This call site is reachable on: 'linux', 'Windows' 10.0 and before. 'Test.UnsupportedOnWindows8IosSupportsIos14_19()' is supported on: 'ios' from version 14.0 to 19.0, 'Windows' 8.0 and later.
            SupportedOnWindowsUntil10AndLinux();
            {|#1:SupportedOnWindowsUntil10()|}; // This call site is reachable on: 'linux'. 'Test.SupportedOnWindowsUntil10()' is only supported on: 'Windows'.
        }
        else
        {
            [|UnsupportedOnWindows8IosSupportsIos14_19()|]; // This call site is reachable on all platforms. 'Test.UnsupportedOnWindows8IosSupportsIos14_19()' is supported on: 'ios' from version 14.0 to 19.0, 'Windows' 8.0 and later.
            [|SupportedOnWindowsUntil10AndLinux()|]; // This call site is reachable on all platforms. 'Test.SupportedOnWindowsUntil10AndLinux()' is only supported on: 'linux', 'Windows' 10.0 and before.
            [|SupportedOnWindowsUntil10()|]; // This call site is reachable on all platforms. 'Test.SupportedOnWindowsUntil10()' is only supported on: 'Windows' 10.0 and before.
        }

        if (_windows8IosNotSupportedSupportedIos14_18)
        {
            UnsupportedOnWindows8IosSupportsIos14_19();
            [|SupportedOnWindowsUntil10AndLinux()|]; // This call site is reachable on: 'ios' 14.0 and later. 'Test.SupportedOnWindowsUntil10AndLinux()' is only supported on: 'linux', 'Windows'.
            [|SupportedOnWindowsUntil10()|]; // This call site is reachable on: 'ios' 14.0 and later. 'Test.SupportedOnWindowsUntil10()' is only supported on: 'Windows'.      
        }
    }

    [UnsupportedOSPlatform(""ios"")]
    [SupportedOSPlatform(""ios14.0"")]
    [UnsupportedOSPlatform(""ios19.0"")]
    [UnsupportedOSPlatform(""Windows8.0"")]
    void UnsupportedOnWindows8IosSupportsIos14_19() { }

    [SupportedOSPlatform(""linux"")]
    [SupportedOSPlatform(""Windows"")]
    [UnsupportedOSPlatform(""Windows10.0"")]
    void SupportedOnWindowsUntil10AndLinux() { }

    [SupportedOSPlatform(""Windows"")]
    [UnsupportedOSPlatform(""Windows10.0"")]
    void SupportedOnWindowsUntil10() { }
}";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0).WithArguments("Test.UnsupportedOnWindows8IosSupportsIos14_19()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "Windows", "8.0"),
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "Windows", "10.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(1).WithArguments("Test.SupportedOnWindowsUntil10()", "'Windows'", "'linux'"));
        }

        [Fact]
        public async Task GuardAttributesFalsePositivesAsync()
        {
            var source = @"
using System;
using System.Diagnostics;
using System.Runtime.Versioning;

class Test
{
    [SupportedOSPlatformGuard(""Windows10.0"")]
    private string IsWindow10Supported() => ""true"";

    [SupportedOSPlatformGuard(""linux"")]
    [SupportedOSPlatformGuard(""Windows10.0"")]
    private bool _linuxAndWindows10Supported;

    [UnsupportedOSPlatformGuard(""linux"")]
    [UnsupportedOSPlatformGuard(""ios9.0"")]
    private bool LinuxIos9NotSupported { get; set;}

    void M1()
    {
        if (IsWindow10Supported() == ""true"") // return type string, no effect
        {
            [|SupportedOnWindows10Linux()|];
            [|SupportedOnWindows8()|];
        }
        _linuxAndWindows10Supported = true; // normal statements no effect
        IsWindow10Supported();
        [|SupportedOnWindows10Linux()|];
        var value = _linuxAndWindows10Supported;
        if (value)
        {
            IsWindow10Supported();
            [|SupportedOnWindows8()|];
            SupportedOnWindows10Linux();
        }
        if (_linuxAndWindows10Supported == true)
        {
            [|SupportedOnWindows8()|];
            [|SupportedOnWindows10Linux()|]; // 39
        }
        var result = LinuxIos9NotSupported == true;  // not a guarding a block
        [|UnsupportedOnLinuxIos91()|];

        Debug.Assert(LinuxIos9NotSupported == false); // no effect when there is expression
        [|UnsupportedOnLinuxIos91()|];

        Debug.Assert(LinuxIos9NotSupported); // Assert should work
        UnsupportedOnLinuxIos91();
    }

    [UnsupportedOSPlatform(""Linux"")]
    [UnsupportedOSPlatform(""ios9.1"")]
    void UnsupportedOnLinuxIos91() { }

    [SupportedOSPlatform(""windows10.0"")]
    [SupportedOSPlatform(""Linux"")]
    void SupportedOnWindows10Linux() { }

    [SupportedOSPlatform(""windows8.0"")]
    void SupportedOnWindows8() { }
}";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task GuardMemberWithinPlatformSpecificTypeShouldNowWarnAsync()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

class Test
{
    void M1()
    {
        [|UnsupportedBrowserType.M1()|];
        [|UnsupportedBrowserType.M2()|];
        if (UnsupportedBrowserType.IsSupported)
        {
            UnsupportedBrowserType.M1();
            UnsupportedBrowserType.M2();
        }
        var t = [|new UnsupportedBrowserType()|];

        [|WindowsOnlyType.M1()|];
        [|WindowsOnlyType.M2()|];
        if (WindowsOnlyType.IsSupported)
        {
            WindowsOnlyType.M1();
            WindowsOnlyType.M2();
        }
        var w = [|new WindowsOnlyType()|];
    }
}

[UnsupportedOSPlatform(""browser"")]
class UnsupportedBrowserType
{
    public static void M1() { }
    [UnsupportedOSPlatformGuard(""browser"")]
    public static bool IsSupported { get; }
    public static void M2() { }
}
[SupportedOSPlatform(""windows"")]
class WindowsOnlyType
{
    public static void M1() { }
    [SupportedOSPlatformGuard(""windows"")]
    public static bool IsSupported { get; }
    public static void M2() { }
}
";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task IosGuardsMacCatalystAsync()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

class Test
{
    [SupportedOSPlatform(""ios"")]
    private void IosSupported() { }

    [SupportedOSPlatform(""maccatalyst"")]
    internal void SupportsMacCatalyst() { }

    [SupportedOSPlatform(""ios"")]
    [SupportedOSPlatform(""Linux"")]
    [SupportedOSPlatform(""maccatalyst"")]
    void SupportedOnIOSLinuxMacCatalyst() { }

    [UnsupportedOSPlatform(""maccatalyst"")]
    static void UnsupportsMacCatalyst() { }

    [UnsupportedOSPlatform(""ios"")]
    static void UnsupportsIos() { }

    [SupportedOSPlatform(""ios"")]
    [UnsupportedOSPlatform(""MacCatalyst"")]
    static void SupportsIOSNotMacCatalyst() { }

    void M1()
    {
        if (OperatingSystem.IsIOS())
        {
            SupportedOnIOSLinuxMacCatalyst();
            [|SupportsMacCatalyst()|]; // This call site is reachable on: 'IOS'. 'Test.SupportsMacCatalyst()' is only supported on: 'maccatalyst'.
            IosSupported();
            [|SupportsIOSNotMacCatalyst()|]; // This call site is reachable on: 'maccatalyst'. 'Test.SupportsIOSNotMacCatalyst()' is only supported on: 'ios'.
            [|UnsupportsIos()|];             // This call site is reachable on: 'maccatalyst'. 'Test.UnsupportsIos()' is unsupported on: 'ios', 'maccatalyst'.
            [|UnsupportsMacCatalyst()|];     // This call site is reachable on: 'maccatalyst'. 'Test.UnsupportsMacCatalyst()' is unsupported on: 'maccatalyst'.
        }

        if (OperatingSystem.IsMacCatalyst())
        {          
            SupportedOnIOSLinuxMacCatalyst();
            SupportsMacCatalyst();
            IosSupported();
            [|SupportsIOSNotMacCatalyst()|]; // This call site is reachable on: 'MacCatalyst'. 'Test.SupportsIOSNotMacCatalyst()' is only supported on: 'ios'.
            [|UnsupportsIos()|];             // This call site is reachable on: 'MacCatalyst'. 'Test.UnsupportsIos()' is unsupported on: 'maccatalyst'.
            [|UnsupportsMacCatalyst()|];     // This call site is reachable on: 'MacCatalyst'. 'Test.UnsupportsMacCatalyst()' is unsupported on: 'maccatalyst'.
        }

        if (OperatingSystem.IsIOS() && !OperatingSystem.IsMacCatalyst())
        {            
            SupportedOnIOSLinuxMacCatalyst();              
            [|SupportsMacCatalyst()|];       // This call site is reachable on: 'IOS'. 'Test.SupportsMacCatalyst()' is only supported on: 'maccatalyst'.     
            IosSupported();
            SupportsIOSNotMacCatalyst();
            [|UnsupportsIos()|];             // This call site is reachable on: 'IOS'. 'Test.UnsupportsIos()' is unsupported on: 'ios'.
            UnsupportsMacCatalyst();      
        }
    }
}";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task IOSSupportGuardAttributesInferMacCatalystAsync()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

class Test
{
    [SupportedOSPlatform(""ios"")]
    private void IosSupported() { }

    [SupportedOSPlatform(""maccatalyst"")]
    internal void SupportsMacCatalyst() { }

    [SupportedOSPlatform(""ios"")]
    [SupportedOSPlatform(""Linux"")]
    [SupportedOSPlatform(""maccatalyst"")]
    void SupportedOnIOSLinuxMacCatalyst() { }

    [UnsupportedOSPlatform(""maccatalyst"")]
    static void UnsupportsMacCatalyst() { }

    [UnsupportedOSPlatform(""ios"")]
    static void UnsupportsIos() { }

    [SupportedOSPlatform(""ios"")]
    [UnsupportedOSPlatform(""MacCatalyst"")]
    static void SupportsIOSNotMacCatalyst() { }

    [SupportedOSPlatformGuard(""ios"")] // maccatalyst inferred
    public bool WorksOnIOS => true;

    [SupportedOSPlatformGuard(""maccatalyst"")] // annotation not needed
    [SupportedOSPlatformGuard(""ios"")] // therefore same as above guard
    public bool WorksOnMacCatalystAndIOS => true;

    [UnsupportedOSPlatformGuard(""maccatalyst"")] // excludes inferred guard
    [SupportedOSPlatformGuard(""ios"")] // therefore only for IOS
    public bool WorksOnIOSNotMacCatalyst => true;

    [SupportedOSPlatformGuard(""maccatalyst"")] // only for mac
    public bool WorksOnMacCatalyst => true;

    void M1()
    {
        if (WorksOnIOS)
        {
            SupportedOnIOSLinuxMacCatalyst();
            [|SupportsMacCatalyst()|]; // This call site is reachable on: 'ios'. 'Test.SupportsMacCatalyst()' is only supported on: 'maccatalyst'.
            IosSupported();
            [|SupportsIOSNotMacCatalyst()|]; // This call site is reachable on: 'maccatalyst'. 'Test.SupportsIOSNotMacCatalyst()' is only supported on: 'ios'.
            [|UnsupportsIos()|];             // This call site is reachable on: 'ios'. 'Test.UnsupportsIos()' is unsupported on: 'ios', 'maccatalyst'.
            [|UnsupportsMacCatalyst()|];     // This call site is reachable on: 'ios', 'maccatalyst'. 'Test.UnsupportsMacCatalyst()' is unsupported on: 'maccatalyst'.
        }

        if (WorksOnMacCatalystAndIOS)
        {
            SupportedOnIOSLinuxMacCatalyst();
            [|SupportsMacCatalyst()|]; // This call site is reachable on: 'ios'. 'Test.SupportsMacCatalyst()' is only supported on: 'maccatalyst'.
            IosSupported();
            [|SupportsIOSNotMacCatalyst()|]; // This call site is reachable on: 'maccatalyst'. 'Test.SupportsIOSNotMacCatalyst()' is only supported on: 'ios'.
            [|UnsupportsIos()|];             // This call site is reachable on: 'ios'. 'Test.UnsupportsIos()' is unsupported on: 'ios', 'maccatalyst'.
            [|UnsupportsMacCatalyst()|];     // This call site is reachable on: 'ios', 'maccatalyst'. 'Test.UnsupportsMacCatalyst()' is unsupported on: 'maccatalyst'.
        }

        if (WorksOnMacCatalyst)
        {          
            SupportedOnIOSLinuxMacCatalyst();
            SupportsMacCatalyst();
            IosSupported();
            [|SupportsIOSNotMacCatalyst()|]; // This call site is reachable on: 'maccatalyst'. 'Test.SupportsIOSNotMacCatalyst()' is only supported on: 'ios'.
            [|UnsupportsIos()|];             // This call site is reachable on: 'maccatalyst'. 'Test.UnsupportsIos()' is unsupported on: 'maccatalyst'.
            [|UnsupportsMacCatalyst()|];     // This call site is reachable on: 'maccatalyst'. 'Test.UnsupportsMacCatalyst()' is unsupported on: 'maccatalyst'.
        }

        if (WorksOnIOSNotMacCatalyst)
        {            
            SupportedOnIOSLinuxMacCatalyst();
            [|SupportsMacCatalyst()|];       // This call site is reachable on all platforms. 'Test.SupportsMacCatalyst()' is only supported on: 'maccatalyst'.    
            IosSupported();                  
            SupportsIOSNotMacCatalyst();
            [|UnsupportsIos()|];             // This call site is reachable on all platforms. 'Test.UnsupportsIos()' is unsupported on: 'ios', 'maccatalyst'.
            UnsupportsMacCatalyst(); 
        }
    }
}";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task IOSUnsupportGuardAttributesInferMacCatalystAsync()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

class Test
{
    [SupportedOSPlatform(""ios"")]
    private void IosSupported() { }

    [SupportedOSPlatform(""maccatalyst"")]
    internal void SupportsMacCatalyst() { }

    [SupportedOSPlatform(""ios"")]
    [SupportedOSPlatform(""maccatalyst"")]
    void SupportedOnIOSMacCatalyst() { }

    [UnsupportedOSPlatform(""maccatalyst"")]
    static void UnsupportsMacCatalyst() { }

    [UnsupportedOSPlatform(""ios"")]
    static void UnsupportsIos() { }

    [SupportedOSPlatform(""ios"")]
    [UnsupportedOSPlatform(""MacCatalyst"")]
    static void SupportsIOSNotMacCatalyst() { }

    [UnsupportedOSPlatformGuard(""ios"")] // // maccatalyst inferred
    public bool DoesNotWorkOnIOS => true;

    [UnsupportedOSPlatformGuard(""ios"")] // same as above guard
    [UnsupportedOSPlatformGuard(""maccatalyst"")] // annotation has no effect
    public bool DoesNotWorkOnIOSAndMacCatalyst => true;

    [UnsupportedOSPlatformGuard(""maccatalyst"")] // only unsupported on macCatalyst
    public bool DoesNotWorkOnMacCatalyst => true;

    void M1()
    {
        if (DoesNotWorkOnIOS)
        {
            [|SupportedOnIOSMacCatalyst()|]; // This call site is reachable on all platforms. 'Test.SupportedOnIOSLinuxMacCatalyst()' is only supported on: 'ios', 'maccatalyst'.
            [|SupportsMacCatalyst()|];       // This call site is reachable on all platforms. 'Test.SupportsMacCatalyst()' is only supported on: 'maccatalyst'.
            [|IosSupported()|];              // This call site is reachable on all platforms. 'Test.IosSupported()' is only supported on: 'ios', 'maccatalyst'.
            [|SupportsIOSNotMacCatalyst()|]; // This call site is reachable on all platforms. 'Test.SupportsIOSNotMacCatalyst()' is only supported on: 'ios'.
            UnsupportsIos();
            UnsupportsMacCatalyst(); 
        }

        if (DoesNotWorkOnIOSAndMacCatalyst)
        {
            [|SupportedOnIOSMacCatalyst()|]; // This call site is reachable on all platforms. 'Test.SupportedOnIOSLinuxMacCatalyst()' is only supported on: 'ios', 'maccatalyst'.
            [|SupportsMacCatalyst()|];       // This call site is reachable on all platforms. 'Test.SupportsMacCatalyst()' is only supported on: 'maccatalyst'.
            [|IosSupported()|];              // This call site is reachable on all platforms. 'Test.IosSupported()' is only supported on: 'ios', 'maccatalyst'.
            [|SupportsIOSNotMacCatalyst()|]; // This call site is reachable on all platforms. 'Test.SupportsIOSNotMacCatalyst()' is only supported on: 'ios'.
            UnsupportsIos();
            UnsupportsMacCatalyst();
        }

        if (DoesNotWorkOnMacCatalyst)
        {          
            [|SupportedOnIOSMacCatalyst()|]; // This call site is reachable on all platforms. 'Test.SupportedOnIOSLinuxMacCatalyst()' is only supported on: 'ios', 'maccatalyst'.
            [|SupportsMacCatalyst()|];       // This call site is reachable on all platforms. 'Test.SupportsMacCatalyst()' is only supported on: 'maccatalyst'.
            [|IosSupported()|];              // This call site is reachable on all platforms. 'Test.IosSupported()' is only supported on: 'ios', 'maccatalyst'.
            [|SupportsIOSNotMacCatalyst()|]; // This call site is reachable on all platforms. 'Test.SupportsIOSNotMacCatalyst()' is only supported on: 'ios'.
            [|UnsupportsIos()|];             // This call site is reachable on all platforms. 'Test.UnsupportsIos()' is unsupported on: 'ios'.
            UnsupportsMacCatalyst();     // This call site is reachable on: 'MacCatalyst'. 'Test.UnsupportsMacCatalyst()' is unsupported on: 'maccatalyst'.
        }
    }
}";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task DynamicallyLoadGuardingVersionFromCallingApiArguments()
        {
            var source = @"
using System;
using System.Diagnostics;
using System.Runtime.Versioning;

class Test
{
    [SupportedOSPlatformGuard(""ios"")]
    private bool IsIosSupported(int major, int minor) => true;

    [UnsupportedOSPlatformGuard(""ios"")]
    private bool IosNotSupportedFrom(int major) => true;

    void M1()
    {
        [|SupportedOniOS11()|]; 
        [|UnsupportedOniOS11()|];

        if (IsIosSupported(11, 0))
        {
            SupportedOniOS11();    
            [|UnsupportedOniOS11()|];
        }
        else
        {
            [|SupportedOniOS11()|];    
            UnsupportedOniOS11();
        }

        if (IosNotSupportedFrom(11))
        {
            [|SupportedOniOS11()|];    
            UnsupportedOniOS11();
        }
        else
        {
            SupportedOniOS11();    
            [|UnsupportedOniOS11()|];
        }
    }

    [SupportedOSPlatform(""ios11.0"")]
    void SupportedOniOS11() { }

    [UnsupportedOSPlatform(""ios11.0"")]
    void UnsupportedOniOS11() { }
}";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task DynamicallyLoadGuardingVersionFromCallingApiArguments_MultipleAttriubtesApplied()
        {
            var source = @"
using System;
using System.Diagnostics;
using System.Runtime.Versioning;

class Test
{
    [SupportedOSPlatformGuard(""ios"")]
    [SupportedOSPlatformGuard(""watchos"")]
    private bool IsIosWatchOsSupported(int major, int minor, int build = 0) => true;

    [SupportedOSPlatformGuard(""ios"")]
    private bool IsIosSupported(int major, int minor, int build = 0) => true;

    [SupportedOSPlatform(""ios13.0.2"")]
    [SupportedOSPlatform(""watchos13.0"")]
    void SupportedOnIosAndWatchOs13() { }

    [SupportedOSPlatform(""ios13.0.2"")]
    void SupportedOnIos13() { }

    [SupportedOSPlatform(""watchos10.0"")]
    void SupportedOnWatchOs10() { }

    void M1()
    {
        if (IsIosWatchOsSupported(13, 0, 2))
        {
            [|SupportedOnIos13()|];    
            [|SupportedOnWatchOs10()|];
            SupportedOnIosAndWatchOs13();
        }

        if (OperatingSystem.IsIOSVersionAtLeast(13, 0, 2) || OperatingSystem.IsWatchOSVersionAtLeast(13))
        {
            [|SupportedOnIos13()|];    
            [|SupportedOnWatchOs10()|];
            SupportedOnIosAndWatchOs13();
        }

        if (IsIosSupported(13, 0, 2))
        {
            SupportedOnIos13();    
            [|SupportedOnWatchOs10()|];
            SupportedOnIosAndWatchOs13();
        }
    }
}";

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task DynamicallyLoadGuardingVersionFromCallingApiArguments_NotWarningCases()
        {
            var source = @"
using System;
using System.Diagnostics;
using System.Runtime.Versioning;

class Test
{
    [SupportedOSPlatformGuard(""ios"")]
    private bool IsIosSupported(int major, string minor, int build = 0) => true; // string parameter not accepted

    [SupportedOSPlatformGuard(""ios"")]
    private bool IsIosSupported(int major, long minor, int build = 0) => true; // long parameter not accepted

    [SupportedOSPlatformGuard(""ios"")]
    private string IsIosSupported(int major, int minor, int build = 0) => ""true""; // not return boolean

    [SupportedOSPlatform(""ios13.0.2"")]
    void SupportedOnIos13() { }

    void M1()
    {
        if (IsIosSupported(13, ""0"", 2))
        {
            [|SupportedOnIos13()|];    
        }

        if (IsIosSupported(13, 0l, 2))
        {
            [|SupportedOnIos13()|];    
        }

        if (IsIosSupported(13, 0, 2) == ""true"")
        {
            [|SupportedOnIos13()|];
        }
    }
}";

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact, WorkItem(4372, "https://github.com/dotnet/roslyn-analyzers/issues/6158")]
        public async Task ChildApiNarrowedParentSupport_GuardingVersionShouldBeComparedWithChildVersion()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

[SupportedOSPlatform(""ios"")]
[SupportedOSPlatform(""tvos"")]
[SupportedOSPlatform(""maccatalyst"")]
class Program
{
    [SupportedOSPlatform(""tvos10.2"")]
    [SupportedOSPlatform(""ios10.3"")]
    [SupportedOSPlatform(""maccatalyst10.3"")]
    public static int P1 => 1;
}
class Test
{
    [SupportedOSPlatform(""ios10.0"")]
    public void M1()
    {
        var rate = (OperatingSystem.IsIOSVersionAtLeast(10, 3) || OperatingSystem.IsMacCatalystVersionAtLeast(10, 3) || OperatingSystem.IsTvOSVersionAtLeast(10, 3))
				    ? Program.P1 : 0; // guarded

        if (OperatingSystem.IsIOSVersionAtLeast(10, 3) || OperatingSystem.IsMacCatalystVersionAtLeast(10, 3) || OperatingSystem.IsTvOSVersionAtLeast(10))
            rate = [|Program.P1|]; // version of TvOS is not guarded
    }
}";

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task ApiAndGuardAttributeBothHasVersions_AttributeVersionWins()
        {
            var source = @"
using System;
using System.Diagnostics;
using System.Runtime.Versioning;

class Test
{
    [SupportedOSPlatformGuard(""ios10.0"")]
    private bool IsIos10Supported(int major, int minor) => true;

    [SupportedOSPlatformGuard(""ios11.0"")]
    private bool IsIos11Supported(int major, int minor) => true;

    [SupportedOSPlatform(""ios11.0"")]
    void SupportedOnIos11() { }

    void M1()
    {
        if (IsIos10Supported(11, 0))
        {
            [|SupportedOnIos11()|];  // Warns because API version 11.0+ is ignored and attribute version ios 10.0+ accounted 
        }

        if (IsIos11Supported(10, 0))
        {
            SupportedOnIos11();  // Not warn because API version 10.0+ is ignored and attribute version ios 11.0+ accounted 
        }
    }
}";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task OneOfSupportsNeedsGuard_AllOtherSuppressedByCallsite()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

[SupportedOSPlatform(""android23.0"")]
[SupportedOSPlatform(""ios13.0"")]
[SupportedOSPlatform(""windows8.1"")]
[UnsupportedOSPlatform(""MacCatalyst13.0"")]
class MyUsage
{
    public void M()
    {
        var t = [|new MyType()|]; // This call site is reachable on: 'android' 23.0 and later, 'ios' 13.0 and later, 'windows' 8.1 and later. 'MyType' is only supported on: 'windows' 10.0.10240 and later.

        if (!OperatingSystem.IsWindows() || OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
        {
            t = new MyType();
        }

        if (!OperatingSystem.IsWindows())
        {
            t = new MyType();
        }

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
        {
            t = new MyType();
        }
    }
}
[SupportedOSPlatform(""android23.0"")]
[SupportedOSPlatform(""ios13.0"")]
[SupportedOSPlatform(""windows10.0.10240"")]
[UnsupportedOSPlatform(""MacCatalyst13.0"")]
class MyType { }
";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task TwoOfSupportsNeedsGuard_AllOtherSuppressedByCallsite()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

[SupportedOSPlatform(""android23.0"")]
[SupportedOSPlatform(""ios13.0"")]
[SupportedOSPlatform(""tvos13.0"")]
[SupportedOSPlatform(""windows8.1"")]
class MyUsage
{
    public void M()
    {
        if (!OperatingSystem.IsWindows() || OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
        {
            var t = [|new MyType()|];
        }

        if (!OperatingSystem.IsWindows())
        {
            var t = [|new MyType()|];
        }

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
        {
            var t = new MyType();
        }
    }
}
[SupportedOSPlatform(""android23.0"")]
[SupportedOSPlatform(""ios13.0"")]
[SupportedOSPlatform(""tvos15.0"")]
[SupportedOSPlatform(""windows10.0.10240"")]
class MyType { }";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task OneOfSupportsNeedsGuard_OneNotSuppressedByCallSite()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

[SupportedOSPlatform(""android23.0"")]
[SupportedOSPlatform(""ios13.0"")]
[SupportedOSPlatform(""windows8.1"")]
class MyUsage
{
    public void M()
    {
        var t = [|new MyType()|];

        if (!OperatingSystem.IsWindows() || OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
        {
            t = [|new MyType()|];
        }

        if (!OperatingSystem.IsWindows())
        {
            t = [|new MyType()|];
        }

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
        {
            t = new MyType();
        }
    }
}

[SupportedOSPlatform(""ios13.0"")]
[SupportedOSPlatform(""windows10.0.10240"")]
class MyType { }";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task OneOfUnsupportsNeedsGuard()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

[UnsupportedOSPlatform(""android23.0"")]
[UnsupportedOSPlatform(""ios13.0"")]
[UnsupportedOSPlatform(""windows10.0.10240"")]
[SupportedOSPlatform(""MacCatalyst13.0"")]
class MyUsage
{
    public void M()
    {
        var t = [|new MyType()|]; // This call site is reachable on: 'windows' 10.0.10240 and before. 'MyType' is unsupported on: 'windows' 8.1 and later.
        if (!OperatingSystem.IsWindows() || !OperatingSystem.IsWindowsVersionAtLeast(8, 1))
        {
            t = new MyType();
        }

        if (!OperatingSystem.IsWindows())
        {
            t = new MyType();
        }

        if (!OperatingSystem.IsWindowsVersionAtLeast(8, 1))
        {
            t = new MyType();
        }
    }
}
[UnsupportedOSPlatform(""android23.0"")]
[UnsupportedOSPlatform(""ios13.0"")]
[UnsupportedOSPlatform(""windows8.1"")]
[SupportedOSPlatform(""MacCatalyst13.0"")]
class MyType { }
";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
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
