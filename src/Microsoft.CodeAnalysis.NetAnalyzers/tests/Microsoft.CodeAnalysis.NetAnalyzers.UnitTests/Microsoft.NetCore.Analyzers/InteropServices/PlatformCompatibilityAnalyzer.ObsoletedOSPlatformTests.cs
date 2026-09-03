// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.PlatformCompatibilityAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.PlatformCompatibilityAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.InteropServices.UnitTests
{
    public partial class PlatformCompatabilityAnalyzerTests
    {
        [Fact]
        public async Task ObsoletedMethodsCalledWarns()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;
using Mock;

public class Test
{
    [SupportedOSPlatform(""Linux"")]
    public void M1()
    {
        ObsoletedOnWindows10(); // Should not warn as only accessible on Linux
        {|#0:ObsoletedOnLinux4()|}; // This call site is reachable on: 'Linux'. 'Test.ObsoletedOnLinux()' is obsoleted on: 'Linux' 4.1 and later.
        {|CA1422:ObsoletedOnLinux4AndWindows10()|}; // This call site is reachable on: 'Linux'. 'Test.ObsoletedOnLinux4AndWindows10()' is obsoleted on: 'Linux' 4.1 and later.
    }
    
    [Mock.ObsoletedOSPlatform(""Linux4.1"")]
    public void ObsoletedOnLinux4() { }

    [Mock.ObsoletedOSPlatform(""Windows10.1.1.1"")]
    public void ObsoletedOnWindows10() { }

    [Mock.ObsoletedOSPlatform(""Linux4.1"", ""Use Linux4Supported"")]
    [Mock.ObsoletedOSPlatform(""Windows10.1.1.1"",""Use Windows10Supported"")]
    public void ObsoletedOnLinux4AndWindows10() { }
}" + MockObsoletedAttributeCS;
            await VerifyAnalyzerCSAsync(csSource, VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.ObsoletedCsReachable).WithLocation(0)
                .WithArguments("Test.ObsoletedOnLinux4()", "'Linux' 4.1 and later", "'Linux'"));

            var vbSource = @"
Imports System
Imports System.Runtime.Versioning
Imports Mock
Public Class Test
    <SupportedOSPlatform(""Linux"")>
    Public Sub M1()
        ObsoletedOnWindows10()
        {|#0:ObsoletedOnLinux()|} ' This call site is reachable on: 'Linux'. 'Public Sub ObsoletedOnLinux()' is obsoleted on: 'Linux' 4.1 and later.
    End Sub

    <ObsoletedOSPlatform(""Windows10.1.1.1"")>
    Public Sub ObsoletedOnWindows10()
    End Sub
    
    <ObsoletedOSPlatform(""Linux4.1"")>
    Public Sub ObsoletedOnLinux()
    End Sub
End Class
" + MockObsoletedAttributeVB;
            await VerifyAnalyzerVBAsync(vbSource, VerifyVB.Diagnostic(PlatformCompatibilityAnalyzer.ObsoletedCsReachable).WithLocation(0)
                .WithArguments("Public Sub ObsoletedOnLinux()", "'Linux' 4.1 and later", "'Linux'"));
        }

        [Fact]
        public async Task ObsoletedAndSupportedMixedDiagnostics()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;
using Mock;

public class Test
{
    public void CrossPlatform()
    {
        {|CA1416:{|CA1422:Supported8Obsoleted10Unsupported11()|}|}; // This call site is reachable on all platforms. 'Test.Supported8Obsoleted10Unsupported11()' is only supported on: 'Windows' from version 8.0 to 11.0.
                                                                    // This call site is reachable on all platforms. 'Test.Supported8Obsoleted10Unsupported11()' is obsoleted on: 'Windows' 10.0 and later.
        {|CA1416:{|CA1422:Supported10.Obsoleted11Unsupported12()|}|}; // This call site is reachable on all platforms. 'Supported10.Obsoleted11Unsupported12()' is only supported on: 'Windows' from version 10.0 to 12.0.
                                                                      // This call site is reachable on all platforms. 'Supported10.Obsoleted11Unsupported12()' is obsoleted on: 'Windows' 11.0 and later.
        {|CA1416:{|CA1422:Supported10.Obsoleted11()|}|}; // This call site is reachable on all platforms. 'Supported10.Obsoleted11()' is only supported on: 'Windows' 10.0 and later.
                                                         // This call site is reachable on all platforms. 'Supported10.Obsoleted11()' is obsoleted on: 'Windows' 11.0 and later.
        {|CA1416:{|CA1422:Supported10.Unsupported11Obsoleted12()|}|}; // This call site is reachable on all platforms. 'Supported10.Unsupported11Obsoleted12()' is only supported on: 'Windows' from version 10.0 to 11.0.
                                                                      // This call site is reachable on all platforms. 'Supported10.Unsupported11Obsoleted12()' is obsoleted on: 'Windows' 12.0 and later.
        {|CA1416:{|CA1422:Unsupported.Obsoleted1Windows10Linux()|}|}; // This call site is reachable on all platforms. 'Unsupported.Obsoleted1Windows10Linux()' is unsupported on: 'Windows'.
                                                                      // This call site is reachable on all platforms. 'Unsupported.Obsoleted1Windows10Linux()' is obsoleted on: 'Linux'.
        {|CA1416:{|CA1422:UnsupportedSupported8.Obsoleted10()|}|}; // This call site is reachable on all platforms. 'UnsupportedSupported8.Obsoleted10()' is unsupported on: 'Windows' 8.0 and before
                                                                   // This call site is reachable on all platforms. 'UnsupportedSupported8.Obsoleted10()' is obsoleted on: 'Windows' 10.0 and later.
        {|CA1416:{|CA1422:UnsupportedSupported8.Obsoleted10Unspported11()|}|}; // This call site is reachable on all platforms. 'UnsupportedSupported8.Obsoleted10Unspported11()' is unsupported on: 'Windows' 8.0 and before.
                                                                               // This call site is reachable on all platforms. 'UnsupportedSupported8.Obsoleted10Unspported11()' is obsoleted on: 'Windows' 10.0 and later.
        {|CA1416:{|CA1422:UnsupportedSupported8.ObsoletedWindows10Linux()|}|}; // This call site is reachable on all platforms. 'UnsupportedSupported8.ObsoletedWindows10Linux()' is unsupported on: 'Windows' 8.0 and before.
                                                                               // This call site is reachable on all platforms. 'UnsupportedSupported8.ObsoletedWindows10Linux()' is obsoleted on: 'Linux', 'Windows' 10.0 and later.
    }
    
    [SupportedOSPlatform(""Windows"")]
    public void WindowsOnly()
    {
        {|CA1416:{|CA1422:Supported8Obsoleted10Unsupported11()|}|}; // This call site is reachable on: 'Windows' all versions. 'Test.Supported8Obsoleted10Unsupported11()' is only supported on: 'Windows' from version 8.0 to 11.0.
                                                                    // This call site is reachable on: 'Windows' all versions. 'Test.Supported8Obsoleted10Unsupported11()' is obsoleted on: 'Windows' 10.0 and later.
        {|CA1416:{|CA1422:Supported10.Obsoleted11Unsupported12()|}|}; // This call site is reachable on: 'Windows' all versions. 'Supported10.Obsoleted11Unsupported12()' is only supported on: 'Windows' from version 10.0 to 12.0.
                                                                      // This call site is reachable on: 'Windows' all versions. 'Supported10.Obsoleted11Unsupported12()' is obsoleted on: 'Windows' 11.0 and later.
        {|CA1416:{|CA1422:Supported10.Obsoleted11()|}|}; // This call site is reachable on: 'Windows' all versions. 'Supported10.Obsoleted11()' is only supported on: 'Windows' 10.0 and later
                                                         // This call site is reachable on: 'Windows' all versions. 'Supported10.Obsoleted11()' is obsoleted on: 'Windows' 11.0 and later.
        {|CA1416:{|CA1422:Supported10.Unsupported11Obsoleted12()|}|}; // This call site is reachable on: 'Windows' all versions. 'Supported10.Unsupported11Obsoleted12()' is only supported on: 'Windows' from version 10.0 to 11.0.
                                                                      // This call site is reachable on: 'Windows' all versions. 'Supported10.Unsupported11Obsoleted12()' is obsoleted on: 'Windows' 12.0 and later.
    }

    [SupportedOSPlatform(""Windows8.0"")]
    [Mock.ObsoletedOSPlatform(""Windows10.0"")]
    [Mock.UnsupportedOSPlatform(""Windows11.0"")]   
    public void Supported8Obsoleted10Unsupported11() { }
}

[SupportedOSPlatform(""Windows10.0"")]
public class Supported10
{
    [Mock.ObsoletedOSPlatform(""Windows11.0"")]
    [Mock.UnsupportedOSPlatform(""Windows12.0"")]   
    public static void Obsoleted11Unsupported12() { }

    [Mock.ObsoletedOSPlatform(""Windows11.0"")] 
    public static void Obsoleted11() { }

    [Mock.ObsoletedOSPlatform(""Windows12.0"")]
    [Mock.UnsupportedOSPlatform(""Windows11.0"")]  
    public static void Unsupported11Obsoleted12() { }
}
[Mock.UnsupportedOSPlatform(""Windows"")]
[SupportedOSPlatform(""Windows8.0"")]
public class UnsupportedSupported8
{
    [Mock.ObsoletedOSPlatform(""Windows10.0"")]
    public static void Obsoleted10() { }

    [Mock.ObsoletedOSPlatform(""Linux"")] 
    [Mock.ObsoletedOSPlatform(""Windows10.0"")] 
    public static void ObsoletedWindows10Linux() { }

    [Mock.ObsoletedOSPlatform(""Windows10.0"")]
    [Mock.UnsupportedOSPlatform(""Windows11.0"")]
    public static void Obsoleted10Unspported11() { }
}
[Mock.UnsupportedOSPlatform(""Windows"")]
public class Unsupported
{
    [Mock.ObsoletedOSPlatform(""Linux"")] 
    [Mock.ObsoletedOSPlatform(""Windows10.0"")] 
    public static void Obsoleted1Windows10Linux() { }
}
" + MockObsoletedAttributeCS;
            await VerifyAnalyzerCSAsync(csSource, s_msBuildPlatforms);
        }

        [Fact]
        public async Task ObsoletedWithMessageUrlCalledWarns()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;
using Mock;

public class Test
{
    public void CrossPlatform()
    {
        {|#0:ObsoletedWithMessageAndUrl()|}; // This call site is reachable on all platforms. 'Test.ObsoletedWithMessageAndUrl()' is obsoleted on: 'Windows' 10.1.1.1 and later (Use other method instead http://www/look.for.more.info).
        {|#1:ObsoletedWithMessage()|};   // This call site is reachable on all platforms. 'Test.ObsoletedWithMessage()' is obsoleted on: 'Windows' 10.1.1.1 and later (Use other method instead).
        {|#2:ObsoletedWithUrl()|};       // This call site is reachable on all platforms. 'Test.ObsoletedWithUrl()' is obsoleted on: 'Windows' 10.1.1.1 and later (http://www/look.for.more.info).
    }
    
    [Mock.ObsoletedOSPlatform(""Windows10.1.1.1"", Url = ""http://www/look.for.more.info"")]
    public void ObsoletedWithUrl() { }
    [Mock.ObsoletedOSPlatform(""Windows10.1.1.1"", ""Use other method instead"", Url = ""http://www/look.for.more.info"")]
    public void ObsoletedWithMessageAndUrl() { }
    [Mock.ObsoletedOSPlatform(""Windows10.1.1.1"", ""Use other method instead"")]
    public void ObsoletedWithMessage() { }
}" + MockObsoletedAttributeCS;
            await VerifyAnalyzerCSAsync(csSource, s_msBuildPlatforms, VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.ObsoletedCsAllPlatforms).WithLocation(0).
                    WithArguments("Test.ObsoletedWithMessageAndUrl()", "'Windows' 10.1.1.1 and later (Use other method instead http://www/look.for.more.info)"),
                  VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.ObsoletedCsAllPlatforms).WithLocation(1).
                    WithArguments("Test.ObsoletedWithMessage()", "'Windows' 10.1.1.1 and later (Use other method instead)"),
                  VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.ObsoletedCsAllPlatforms).WithLocation(2).
                    WithArguments("Test.ObsoletedWithUrl()", "'Windows' 10.1.1.1 and later (http://www/look.for.more.info)"));
        }

        [Fact]
        public async Task ObsoletedAPIsCAlledFromDifferentCallsite()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;
using Mock;

public class Test
{
    [SupportedOSPlatform(""IOS"")]
    public void CallsiteReachableOnIOS()
    {
        ObsoletedOnWindows(); // Unreachable on windows, not warn
        ObsoletedWithMessage();
        {|CA1422:ObsoletedOnIOS14()|}; // This call site is reachable on: 'IOS', 'maccatalyst'. 'Test.ObsoletedOnIOS14()' is obsoleted on: 'ios' 14.0 and later, 'maccatalyst' 14.0 and later.
    }

    [Mock.UnsupportedOSPlatform(""ios13.0"")]
    [System.Runtime.Versioning.UnsupportedOSPlatform(""Windows10.1.0"")]
    public void SuppressedByCallsiteUnsupported()
    {
        ObsoletedOnWindows(); // Not supported on windows with matching version, not warn
        ObsoletedWithMessage(); // Same as above
        ObsoletedOnIOS14();
    }

    [Mock.UnsupportedOSPlatform(""ios15.0"")]
    [System.Runtime.Versioning.UnsupportedOSPlatform(""Windows11.1.0"")]
    public void NotSuppressedByCallsiteUnsupported() // obsoleted before unsupported version
    {
        {|CA1422:ObsoletedOnWindows()|}; // This call site is reachable on: 'Windows' 11.1.0 and before. 'Test.ObsoletedOnWindows()' is obsoleted on: 'Windows' 10.1.1.1 and later.
        {|CA1422:ObsoletedWithMessage()|}; // This call site is reachable on: 'Windows' 11.1.0 and before. 'Test.ObsoletedWithMessage()' is obsoleted on: 'Windows' 10.1.1.1 and later (Use other method instead).
        {|CA1422:ObsoletedOnIOS14()|}; // This call site is reachable on: 'ios' 15.0 and before, 'maccatalyst' 15.0 and before. 'Test.ObsoletedOnIOS14()' is obsoleted on: 'ios' 14.0 and later, 'maccatalyst' 14.0 and later.
    }

    [Mock.ObsoletedOSPlatform(""ios13.0"")]
    [Mock.ObsoletedOSPlatform(""Windows10.1.0"")]
    public void SuppressedByCallsiteObsoleted()
    {
        ObsoletedOnWindows(); // All calls Obsoleted within version range, not warn
        ObsoletedWithMessage();
        ObsoletedOnIOS14();
    }

    [Mock.ObsoletedOSPlatform(""ios16.0"")]
    [Mock.ObsoletedOSPlatform(""Windows11.1.0"")]
    public void NotSuppressedByCallsiteObsoleted()
    {
        {|CA1422:ObsoletedOnWindows()|}; //This call site is reachable on all platforms. 'Test.ObsoletedOnWindows()' is obsoleted on: 'Windows' 10.1.1.1 and later.
        {|CA1422:ObsoletedWithMessage()|}; // This call site is reachable on all platforms. 'Test.ObsoletedWithMessage()' is obsoleted on: 'Windows' 10.1.1.1 and later (Use other method instead).
        {|CA1422:ObsoletedOnIOS14()|}; // This call site is reachable on all platforms. 'Test.ObsoletedOnIOS14()' is obsoleted on: 'ios' 14.0 and later, 'maccatalyst' 14.0 and later.
    }
    
    [Mock.ObsoletedOSPlatform(""ios14.0"")]
    public void ObsoletedOnIOS14() { }

    [Mock.ObsoletedOSPlatform(""Windows10.1.1.1"")]
    public void ObsoletedOnWindows() { }
    [Mock.ObsoletedOSPlatform(""Windows10.1.1.1"", ""Use other method instead"")]
    public void ObsoletedWithMessage() { }
}" + MockObsoletedAttributeCS;
            await VerifyAnalyzerCSAsync(csSource, s_msBuildPlatforms);
        }

        [Fact]
        public async Task UnsuportedWithMessageCalledWarnsAsync()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;
using Mock;

public class Test
{
    public void CrossPlatform()
    {
        [|UnsupportedIosBrowserWatchOS()|]; // This call site is reachable on all platforms. 'Test.UnsupportedIosBrowserWatchOS()' is unsupported on: 'Browser' (Use BrowserSupported() method instead),
                                                            // 'ios' 13.0 and later (Use Test.IOsSupported() method instead), 'maccatalyst' 13.0 and later (Use Test.IOsSupported() method instead).
        UnsupportedAndroid(); // Cross platform and android not in the MSBuild list so not warn
    }

    [SupportedOSPlatform(""Android"")]
    [SupportedOSPlatform(""browser"")]
    public void ReachableOnAndroidAndBrowser()
    {
        [|UnsupportedIosBrowserWatchOS()|]; // This call site is reachable on: 'Android', 'browser'. 'Test.UnsupportedIosBrowserWatchOS()' is unsupported on: 'Browser' (Use BrowserSupported() method instead).
        [|UnsupportedAndroid()|]; // This call site is reachable on: 'Android' all versions, 'browser'. 'Test.UnsupportedAndroid()' is unsupported on: 'Android' 21.0 and later (Use other method instead).
    }

    [Mock.UnsupportedOSPlatform(""Android21.0"", ""Use other method instead"")]
    public void UnsupportedAndroid() { }

    [Mock.UnsupportedOSPlatform(""ios13.0"", ""Use Test.IOsSupported() method instead"")]
    [Mock.UnsupportedOSPlatform(""Browser"", ""Use BrowserSupported() method instead"")]
    [Mock.UnsupportedOSPlatform(""Watchos"", ""Use WitchSupported() method instead"")]
    public void UnsupportedIosBrowserWatchOS() { }
}" + MockObsoletedAttributeCS;
            await VerifyAnalyzerCSAsync(csSource, s_msBuildPlatforms);
        }

        [Fact]
        public async Task ObsoletedWarningsGuardedWithOperatingSystemAPIs()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;
using Mock;

public class Test
{
    public void CrossPlatform()
    {
        if (OperatingSystem.IsMacOS())
        {
            {|CA1422:ObsoletedOnMacOS()|}; // This call site is reachable on: 'macOS/OSX'. 'Test.ObsoletedOnMacOS()' is obsoleted on: 'macOS/OSX'.
            ObsoletedOnAndroid21(); // Call site only reachable on MacOS, no warning 
            ObsoletedOnLinux4AndWindows10(); // Same, no warning
        }
        else
        {
            ObsoletedOnMacOS();
            ObsoletedOnAndroid21(); // Android is not in MSBuild support list, no warning
            {|CA1422:ObsoletedOnLinux4AndWindows10()|}; // This call site is reachable on all platforms. 'Test.ObsoletedOnLinux4AndWindows10()' is obsoleted on: 'Linux' 4.1 and later, Use Linux4Supported, 'Windows' 10.1.1.1 and later (Use Windows10Supported).
        }

        if (OperatingSystem.IsMacOSVersionAtLeast(11))
        {
            {|CA1422:ObsoletedOnMacOS()|}; // This call site is reachable on: 'macOS/OSX' 11.0 and later. 'Test.ObsoletedOnMacOS()' is obsoleted on: 'macOS/OSX' all versions.
        }
        else
        {
            {|CA1422:ObsoletedOnMacOS()|}; // It could be macos with less version, so warns: This call site is reachable on all platforms. 'Test.ObsoletedOnMacOS()' is obsoleted on: 'macOS/OSX'.
        }
    }
    
    [Mock.ObsoletedOSPlatform(""Android21.0"")]
    public void ObsoletedOnAndroid21() { }

    [Mock.ObsoletedOSPlatform(""MacOS"")]
    public void ObsoletedOnMacOS() { }

    [Mock.ObsoletedOSPlatform(""Linux4.1"", ""Use Linux4Supported"")]
    [Mock.ObsoletedOSPlatform(""Windows10.1.1.1"",""Use Windows10Supported"")]
    public void ObsoletedOnLinux4AndWindows10() { }
}" + MockObsoletedAttributeCS;
            await VerifyAnalyzerCSAsync(csSource, s_msBuildPlatforms);
        }

        [Fact]
        public async Task ObsoletedWarningsGuardedWithOperatingSystemAPIsFromDifferentCallsite()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;
using Mock;

public class Test
{
    [SupportedOSPlatform(""Android"")]
    public void AndroidOnly()
    {
        if (OperatingSystem.IsMacOS())
        {
            ObsoletedOnMacOS(); // The method is not reachable on MacOS, so no warning
            ObsoletedOnAndroid21(); // Conditional only reachable on MacOS, no warning 
            ObsoletedOnLinux4AndWindows10();
        }
        else
        {
            ObsoletedOnMacOS(); // Method only for android, no warning
            {|CA1422:ObsoletedOnAndroid21()|}; // This call site is reachable on: 'Android'. 'Test.ObsoletedOnAndroid21()' is obsoleted on: 'Android' 21.0 and later.
            ObsoletedOnLinux4AndWindows10(); // Method only for android
        }
    }

    [SupportedOSPlatform(""Android"")]
    [SupportedOSPlatform(""MacOS"")]
    public void AndroidMacOSOnly()
    {
        if (OperatingSystem.IsMacOS())
        {
            {|CA1422:ObsoletedOnMacOS()|}; //  This call site is reachable on: 'Android', 'macOS/OSX'. 'Test.ObsoletedOnMacOS()' is obsoleted on: 'macOS/OSX'.
            ObsoletedOnAndroid21();
            ObsoletedOnLinux4AndWindows10();
        }
        else
        {
            ObsoletedOnMacOS();
            {|CA1422:ObsoletedOnAndroid21()|}; // This call site is reachable on: 'Android', 'macOS/OSX'. 'Test.ObsoletedOnAndroid21()' is obsoleted on: 'Android' 21.0 and later.
            ObsoletedOnLinux4AndWindows10();
        }
    }
    
    [Mock.ObsoletedOSPlatform(""Android21.0"")]
    public void ObsoletedOnAndroid21() { }

    [Mock.ObsoletedOSPlatform(""MacOS"")]
    public void ObsoletedOnMacOS() { }

    [Mock.ObsoletedOSPlatform(""Linux4.1"", ""Use Linux4Supported"")]
    [Mock.ObsoletedOSPlatform(""Windows10.1.1.1"",""Use Windows10Supported"")]
    public void ObsoletedOnLinux4AndWindows10() { }
}" + MockObsoletedAttributeCS;
            await VerifyAnalyzerCSAsync(csSource, s_msBuildPlatforms);
        }

        [Fact]
        public async Task ObsoletedWarningsGuardedWithUnsupportedOSPlatformGuardAttribute()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;
using Mock;

public class Test
{
    [UnsupportedOSPlatformGuard(""MacOS"")]
    private readonly bool _macOSNotSupported;

    [SupportedOSPlatformGuard(""Windows11.0"")]
    public bool IsWindows11Supported { get; }

    [UnsupportedOSPlatformGuard(""linux"")]
    [UnsupportedOSPlatformGuard(""Windows10.0"")]
    private bool IsLinuxWindows10NotSupported() => true;

    public void CrossPlatform()
    {
        if (_macOSNotSupported)
        {
            ObsoletedOnMacOS(); // Guarded with the attributed field
            ObsoletedOnMacOS15();
            {|CA1422:ObsoletedOnLinuxAndWindows10()|}; // This call site is reachable on all platforms. 'Test.ObsoletedOnLinux4AndWindows10()' is obsoleted on: 'Linux', 'Windows' 10.1.1.1 and later (Use Windows10Supported).
        }

        if (IsWindows11Supported)
        {
            ObsoletedOnMacOS(); // reachable on 'Windows' 11.0 and later only
            ObsoletedOnMacOS15();
            {|CA1422:ObsoletedOnLinuxAndWindows10()|}; // This call site is reachable on: 'Windows' 11.0 and later. 'Test.ObsoletedOnLinux4AndWindows10()' is obsoleted on: 'Windows' 10.1.1.1 and later (Use Windows10Supported).
        }

        if (IsLinuxWindows10NotSupported())
        {
            {|CA1422:ObsoletedOnMacOS()|}; // This call site is reachable on all platforms. 'Test.ObsoletedOnMacOS()' is obsoleted on: 'macOS/OSX'.
            {|CA1422:ObsoletedOnMacOS15()|}; // This call site is reachable on all platforms. 'Test.ObsoletedOnMacOS15()' is obsoleted on: 'macOS/OSX' 15.0 and later.
            ObsoletedOnLinuxAndWindows10(); // Guarded with the attrubuted API
        }
    }
    
    [Mock.ObsoletedOSPlatform(""MacOS15.0"")]
    public void ObsoletedOnMacOS15() { }

    [Mock.ObsoletedOSPlatform(""MacOS"")]
    public void ObsoletedOnMacOS() { }

    [Mock.ObsoletedOSPlatform(""Linux"", ""Use LinuxSupported"")]
    [Mock.ObsoletedOSPlatform(""Windows10.1.1.1"",""Use Windows10Supported"")]
    public void ObsoletedOnLinuxAndWindows10() { }
}" + MockObsoletedAttributeCS;
            await VerifyAnalyzerCSAsync(csSource, s_msBuildPlatforms);
        }

        [Fact]
        public async Task CalledApiHasSupportedAndObsoletedAttributes_CallsiteSupressesSupportedAttributeWarnsForObsoletedOnly()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

class Program
{
    [Mock.ObsoletedOSPlatform(""ios7.0"", ""Use 'NSString.GetBoundingRect (CGSize, NSStringDrawingOptions, UIStringAttributes, NSStringDrawingContext)' instead."")]
    [Mock.ObsoletedOSPlatform(""maccatalyst7.0"", ""Use 'NSString.GetBoundingRect (CGSize, NSStringDrawingOptions, UIStringAttributes, NSStringDrawingContext)' instead."")]
    [SupportedOSPlatform(""ios"")]
    [SupportedOSPlatform(""maccatalyst"")]
    public static void M3() { }
    
    [SupportedOSPlatform(""ios"")]
    public static void M1()
    {
         {|CA1422:M3()|}; // This call site is reachable on: 'ios', 'maccatalyst'. 'Program.M3()' is obsoleted on: 'ios' 7.0 and later (Use 'NSString.GetBoundingRect (CGSize, NSStringDrawingOptions, UIStringAttributes, NSStringDrawingContext)' instead.), 'maccatalyst' 7.0 and later (Use 'NSString.GetBoundingRect (CGSize, NSStringDrawingOptions, UIStringAttributes, NSStringDrawingContext)' instead.).
    }

    [SupportedOSPlatform(""ios10.0"")]
    public static void M2()
    {
         {|CA1422:M3()|}; // This call site is reachable on: 'ios' 10.0 and later, 'maccatalyst' 10.0 and later. 'Program.M3()' is obsoleted on: 'ios' 7.0 and later (Use 'NSString.GetBoundingRect (CGSize, NSStringDrawingOptions, UIStringAttributes, NSStringDrawingContext)' instead.), 'maccatalyst' 7.0 and later (Use 'NSString.GetBoundingRect (CGSize, NSStringDrawingOptions, UIStringAttributes, NSStringDrawingContext)' instead.).
    }
}" + MockObsoletedAttributeCS;

            await VerifyAnalyzerCSAsync(source);
        }

        private readonly string MockObsoletedAttributeCS = @"
namespace Mock
{
    public abstract class OSPlatformAttribute : Attribute
    {
        private protected OSPlatformAttribute(string platformName)
        {
            PlatformName = platformName;
        }
        public string PlatformName { get; }
    }

    [AttributeUsage(AttributeTargets.Assembly |
                    AttributeTargets.Class |
                    AttributeTargets.Constructor |
                    AttributeTargets.Enum |
                    AttributeTargets.Event |
                    AttributeTargets.Field |
                    AttributeTargets.Interface |
                    AttributeTargets.Method |
                    AttributeTargets.Module |
                    AttributeTargets.Property |
                    AttributeTargets.Struct,
                    AllowMultiple = true, Inherited = false)]
    public sealed class UnsupportedOSPlatformAttribute : OSPlatformAttribute
    {
        public UnsupportedOSPlatformAttribute(string platformName) : base(platformName)
        {
        }
        public UnsupportedOSPlatformAttribute(string platformName, string message) : base(platformName)
        {
            Message = message;
        }
        public string Message { get; }
    }

    [AttributeUsage(AttributeTargets.Assembly |
                    AttributeTargets.Class |
                    AttributeTargets.Constructor |
                    AttributeTargets.Enum |
                    AttributeTargets.Event |
                    AttributeTargets.Field |
                    AttributeTargets.Interface |
                    AttributeTargets.Method |
                    AttributeTargets.Module |
                    AttributeTargets.Property |
                    AttributeTargets.Struct,
                    AllowMultiple = true, Inherited = false)]
    public sealed class ObsoletedOSPlatformAttribute : OSPlatformAttribute
    {
        public ObsoletedOSPlatformAttribute(string platformName) : base(platformName)
        {
        }
        public ObsoletedOSPlatformAttribute(string platformName, string message) : base(platformName)
        {
            Message = message;
        }
        public string Message { get; }
        public string Url { get; set; }
    }
}";

        private readonly string MockObsoletedAttributeVB = @"
Namespace Mock
    <AttributeUsage(AttributeTargets.Assembly Or
                    AttributeTargets.[Class] Or AttributeTargets.Constructor Or
                    AttributeTargets.[Enum] Or AttributeTargets.[Event] Or
                    AttributeTargets.Field Or AttributeTargets.[Interface] Or
                    AttributeTargets.Method Or AttributeTargets.[Module] Or
                    AttributeTargets.[Property] Or
                    AttributeTargets.Struct,
                    AllowMultiple:=True, Inherited:=False)>
    Public NotInheritable Class ObsoletedOSPlatformAttribute
        Inherits Attribute

        Public Sub New(ByVal platformName As String)
            PlatformName = platformName
        End Sub

        Public Sub New(ByVal platformName As String, ByVal message As String)
            PlatformName = platformName
            Message = message
        End Sub

        Public ReadOnly Property PlatformName As String
        Public ReadOnly Property Message As String
        Public Property Url As String
    End Class
End Namespace";
    }
}
