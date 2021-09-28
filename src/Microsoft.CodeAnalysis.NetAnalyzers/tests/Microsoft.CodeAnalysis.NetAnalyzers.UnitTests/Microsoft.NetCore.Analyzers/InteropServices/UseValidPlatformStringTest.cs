// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.UseValidPlatformString,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.UseValidPlatformString,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.InteropServices.UnitTests
{
    public class UseValidPlatformStringTest
    {
        private const string s_msBuildPlatforms = @"is_global = true
build_property._SupportedPlatformList=CustomPlatform";

        [Fact]
        public async Task ValidPlatformStringUsedNotWarnAsync()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;

public class Test
{
    [SupportedOSPlatform(""Browser"")]
    public void SupportedBrowser() { }

    [SupportedOSPlatform(""iOS"")]
    public string SupportedIos => string.Empty;

    [UnsupportedOSPlatform(""linux"")]
    public void UnsupportedLinux() { }

    [SupportedOSPlatform(""Android"")]
    public event EventHandler AndroidOnlyEvent;

    [UnsupportedOSPlatform(""macos"")]
    public void UnsupportedMacOS() { }

    [SupportedOSPlatform(""TVOS"")]
    public Test() { }

    [SupportedOSPlatform(""freeBsd2.0.1.2"")]
    public void SupportedFreeBsd() { }

    [SupportedOSPlatform(""watchOS3.2.1"")]
    public void SupporteWatchOS() { }

    [UnsupportedOSPlatform(""windows7.0"")]
    public static void SupportedWindows() { }
}";
            await VerifyAnalyzerCsAsync(csSource);
        }

        [Fact]
        public async Task ValidPlatformStringUsedNotWarnVBAsync()
        {
            var vbSource = @"
Imports System.Runtime.Versioning

Public Class Test
    <SupportedOSPlatform(""Browser"")>
    Public Sub SupportedBrowser()
    End Sub

    <SupportedOSPlatform(""iOS"")>
    Public Sub SupportedIos()
    End Sub

    <UnsupportedOSPlatform(""linux"")>
    Public Sub UnsupportedLinux()
    End Sub

    <SupportedOSPlatform(""Android"")>
    Public Sub SupportedAndroid()
    End Sub

    <UnsupportedOSPlatform(""macos"")>
    Public Sub UnsupportedMacOS()
    End Sub

    <SupportedOSPlatform(""tvos"")>
    Public Sub SupportedTvOS()
    End Sub

    <SupportedOSPlatform(""freeBsd2.0.1.2"")>
    Public Sub SupportedFreeBsd()
    End Sub

    <SupportedOSPlatform(""watchOS3.2.1"")>
    Public Sub SupporteWatchOS()
    End Sub

    <UnsupportedOSPlatform(""WINDOWS7.0"")>
    Public Shared Sub SupportedWindows()
    End Sub
End Class";
            await VerifyAnalyzerAsyncVbAsync(vbSource);
        }

        [Fact]
        public async Task InvalidPlatformNameOrVersionWarnsAsync()
        {
            var csSource = @"
using System.Runtime.Versioning;

public class Test
{
    [{|#0:UnsupportedOSPlatform(""window7.0"")|}] // The platform 'window' is not a known platform name
    public void InvlaidPlatform() { }

    [{|#1:SupportedOSPlatform(""watch7.0"")|}] // The platform 'watch' is not a known platform name
    public void InvlaidPlatform2() { }

    [{|#2:UnsupportedOSPlatform(""windows7"")|}] // Version '7' is not valid for platform 'windows'. Use a version with 2-4 parts for this platform.
    public static void InvalidVersion() { }

    [{|#3:SupportedOSPlatform(""watchOs7.1.0.2"")|}] // Version '7.1.0.2' is not valid for platform 'watchOs'. Use a version with 2-3 parts for this platform.
    public static void InvalidVersion2() { }

    [{|#4:UnsupportedOSPlatform(""browser1.0."")|}] // Version '1.0.' is not valid for platform 'browser'. Do not use versions for this platform.
    public static void InvalidVersion3() { }
}";
            await VerifyAnalyzerCsAsync(csSource,
                VerifyCS.Diagnostic(UseValidPlatformString.UnknownPlatform).WithLocation(0).WithArguments("window"),
                VerifyCS.Diagnostic(UseValidPlatformString.UnknownPlatform).WithLocation(1).WithArguments("watch"),
                VerifyCS.Diagnostic(UseValidPlatformString.InvalidVersion).WithLocation(2).WithArguments("7", "windows", "-4"),
                VerifyCS.Diagnostic(UseValidPlatformString.InvalidVersion).WithLocation(3).WithArguments("7.1.0.2", "watchOs", "-3"),
                VerifyCS.Diagnostic(UseValidPlatformString.NoVersion).WithLocation(4).WithArguments("1.0.", "browser"));
        }

        [Fact]
        public async Task InvalidPlatformNameOrVersionWarnsVBAsync()
        {
            var vbSource = @"
Imports System.Runtime.Versioning

Public Class Test
    <[|UnsupportedOSPlatform(""window7.0"")|]>
    Public Sub InvlaidPlatform()
    End Sub

    <[|SupportedOSPlatform(""watch7.0"")|]>
    Public Sub InvlaidPlatform2()
    End Sub

    <[|UnsupportedOSPlatform(""windows7"")|]>
    Public Shared Sub InvalidVersion()
    End Sub

    <[|SupportedOSPlatform(""watchOs7.1.0.2.3"")|]>
    Public Shared Sub InvalidVersion2()
    End Sub

    <[|UnsupportedOSPlatform(""browser1.0."")|]>
    Public Shared Sub InvalidVersion3()
    End Sub
End Class";
            await VerifyAnalyzerAsyncVbAsync(vbSource);
        }

        [Fact]
        public async Task InvalidPlatformNameOrVersionDifferentSymbolsWarnsAsync()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;

[assembly:[|UnsupportedOSPlatform(""blazor"")|]] // The platform 'blazor' is not a known platform name
namespace ns
{
    [[|UnsupportedOSPlatform(""razor"")|]] // The platform 'razor' is not a known platform name
    public class Test
    {
        [[|UnsupportedOSPlatform(""window7.0"")|]] // The platform 'window' is not a known platform name
        public string InvlaidPlatform => string.Empty;

        [[|SupportedOSPlatform(""watch7.0"")|]] // The platform 'watch' is not a known platform name
        public Test() { }

        [[|UnsupportedOSPlatform(""windows7"")|]] // Version '7' is not valid for platform 'windows'. Use a version with 2-4 parts for this platform.
        public event EventHandler Windows7Event;

        [[|SupportedOSPlatform(""watchOs7.1.0.2.3"")|]] // Version '7.1.0.2.3' is not valid for platform 'watchOs'. Use a version with 2-3 parts for this platform.
        private static int s_field = 0;
    }
}";
            await VerifyAnalyzerCsAsync(csSource);
        }

        [Fact]
        public async Task NotWarnForCustomPlatformAddedToMsBuildAsync()
        {
            var csSource = @"
using System.Runtime.Versioning;

public class Test
{
    [SupportedOSPlatform(""customPlatform"")]
    public void SupportedCustomPlatform() { }

    [SupportedOSPlatform(""customplatform1.0"")]
    public void SupportedCustomPlatform1() { }

    [UnsupportedOSPlatform(""customPlatform2.0"")]
    public void UnsupportedCustomPlatform2() { }

    [UnsupportedOSPlatform(""customplatform"")]
    public void UnsupportedCustomPlatform() { }

    [[|UnsupportedOSPlatform(""custom1.0"")|]] // The platform 'custom' is not a known platform name
    public void InvlaidPlatform() { }
}";
            await VerifyAnalyzerCsAsync(csSource, s_msBuildPlatforms);
        }

        [Fact]
        public async Task NotWarnForCustomPlatformAddedToMsBuildVBAsync()
        {
            var vbSource = @"
Imports System.Runtime.Versioning

Public Class Test
    <SupportedOSPlatform(""customPlatform"")>
    Public Sub SupportedCustomPlatform()
    End Sub

    <SupportedOSPlatform(""customplatform1.0"")>
    Public Sub SupportedCustomPlatform1()
    End Sub

    <UnsupportedOSPlatform(""customPlatform2.0"")>
    Public Sub UnsupportedCustomPlatform2()
    End Sub

    <UnsupportedOSPlatform(""customplatform"")>
    Public Sub UnsupportedCustomPlatform()
    End Sub

    <[|UnsupportedOSPlatform(""custom1.0"")|]>
    Public Sub InvlaidPlatform()
    End Sub
End Class";
            await VerifyAnalyzerAsyncVbAsync(vbSource, s_msBuildPlatforms);
        }

        [Fact]
        public async Task IsOsPlatformWithInvalidPlatformWarnsAsync()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;
using System.Diagnostics;

public class Test
{
    public void M1()
    {
        if (OperatingSystem.IsOSPlatform([|""x""|])) // The platform 'x' is not a known platform name
            PlatformSpecificAPI();

        if (OperatingSystem.IsOSPlatformVersionAtLeast([|""x""|], 1)) // The platform 'x' is not a known platform name
            PlatformSpecificAPI();

        if (!OperatingSystem.IsOSPlatformVersionAtLeast([|""window""|], 8, 100)) // The platform 'window' is not a known platform name
            PlatformSpecificAPI();

        if (!OperatingSystem.IsOSPlatformVersionAtLeast([|""xPlatform""|], 1, 2, 3, 4)) // The platform 'xPlatform' is not a known platform name
            PlatformSpecificAPI();

        if (OperatingSystem.IsOSPlatform(""windows"")) // No warn
            PlatformSpecificAPI();

        if (OperatingSystem.IsOSPlatformVersionAtLeast(""ios"", 12, 100, 0, 1)) // No warn
            PlatformSpecificAPI();

        if (OperatingSystem.IsOSPlatform([|""windows8.0""|])) // Version part not allowed: The platform 'windows8.0' is not a known platform name
            PlatformSpecificAPI();

        Debug.Assert(OperatingSystem.IsOSPlatformVersionAtLeast([|""windows8.0""|], 1, 2, 3, 4));
        Debug.Assert(OperatingSystem.IsOSPlatform([|""invalid""|]));
    }

    public void PlatformSpecificAPI() { }
}";
            await VerifyAnalyzerCsAsync(csSource);
        }

        [Fact]
        public async Task IsOsPlatformWithInvalidPlatformWarnsVBAsync()
        {
            var vbSource = @"
Imports System
Imports System.Runtime.Versioning

Public Class Test
    Public Sub M1()
        If OperatingSystem.IsOSPlatform([|""x""|]) Then PlatformSpecificAPI()
        If OperatingSystem.IsOSPlatformVersionAtLeast([|""x""|], 1) Then PlatformSpecificAPI()
        If Not OperatingSystem.IsOSPlatformVersionAtLeast([|""window""|], 8, 100) Then PlatformSpecificAPI()
        If OperatingSystem.IsOSPlatform(""windows"") Then PlatformSpecificAPI()
        If OperatingSystem.IsOSPlatformVersionAtLeast(""ios"", 12, 100, 0, 1) Then PlatformSpecificAPI()
        If OperatingSystem.IsOSPlatform([|""windows8.0""|]) Then PlatformSpecificAPI()
    End Sub

    Public Sub PlatformSpecificAPI()
    End Sub
End Class";
            await VerifyAnalyzerAsyncVbAsync(vbSource);
        }

        [Fact]
        public async Task MoreInvalidPlatformStringsWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    [[|SupportedOSPlatform(""Ios-4.1"")|]]
    public void SupportedOSPlatformIosDash4_1() { }

    [[|UnsupportedOSPlatform(""Ios*4.1"")|]]
    public void UnsupportedOSPlatformIosStar4_1() { }

    [[|SupportedOSPlatform(null)|]]
    public void SupportedOSPlatformWithNullString() { }

    [[|SupportedOSPlatform(""Linux_4.1"")|]]
    public void SupportedLinux_41() { }

    [[|UnsupportedOSPlatform("""")|]]
    public void UnsupportedWithEmptyString() { }
}";
            await VerifyAnalyzerCsAsync(source);
        }

        [Fact]
        public async Task InvalidVersionPartInPlatformStringsWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    [{|#0:SupportedOSPlatform(""Android4.8.1.2.3"")|}] // Version '4.8.1.2.3' is not valid for platform 'Android'. Use a version with 2-4 parts for this platform.
    public void SupportedOSPlatformAndroid5Parts() { }

    [{|#1:UnsupportedOSPlatform(""Ios14.1.2.3"")|}] // Version '14.1.2.3' is not valid for platform 'Ios'. Use a version with 2-3 parts for this platform.
    public void UnsupportedOSPlatformIos4PartsInvalid() { }

    [{|#2:SupportedOSPlatform(""macos1.2.3.4.5"")|}] // Version '1.2.3.4.5' is not valid for platform 'macos'. Use a version with 2-3 parts for this platform.
    public void SupportedOSPlatformMac5PartsInvalid() { }

    [SupportedOSPlatform(""macos1.2.3"")]
    public void SupportedMacOs3PartValid() { }

    [SupportedOSPlatform(""tvos1.2.3"")]
    public void SupportedTvOs3PartValid() { }

    [{|#3:UnsupportedOSPlatform(""watchos1.2.3.4"")|}] // Version '1.2.3.4' is not valid for platform 'watchos'. Use a version with 2-3 parts for this platform.
    public void UnsupportedWatchOs4PartsInvalid() { }

    [{|#4:SupportedOSPlatform(""Linux4.1"")|}] // Version '4.1' is not valid for platform 'Linux'. Do not use versions for this platform.
    public void SupportedLinux_41() { }
}";
            await VerifyAnalyzerCsAsync(source,
                VerifyCS.Diagnostic(UseValidPlatformString.InvalidVersion).WithLocation(0).WithArguments("4.8.1.2.3", "Android", "-4"),
                VerifyCS.Diagnostic(UseValidPlatformString.InvalidVersion).WithLocation(1).WithArguments("14.1.2.3", "Ios", "-3"),
                VerifyCS.Diagnostic(UseValidPlatformString.InvalidVersion).WithLocation(2).WithArguments("1.2.3.4.5", "macos", "-3"),
                VerifyCS.Diagnostic(UseValidPlatformString.InvalidVersion).WithLocation(3).WithArguments("1.2.3.4", "watchos", "-3"),
                VerifyCS.Diagnostic(UseValidPlatformString.NoVersion).WithLocation(4).WithArguments("4.1", "Linux"));
        }

        [Fact]
        public async Task PlatformOSXIsAliasForMacOSAsync()
        {
            var csSource = @"
using System.Runtime.Versioning;

public class Test
{
    [{|#0:SupportedOSPlatform(""MacOS1.2.3.4"")|}] // Version '1.2.3.4' is not valid for platform 'MacOS'. Use a version with 2-3 parts for this platform.
    public void SupportedOSPlatformMac4PartsInvalid() { }

    [SupportedOSPlatform(""MacOS1.2"")]
    public void SupportedMacOs2PartsValid() { }

    [{|#1:SupportedOSPlatform(""OSX1.2.3.4"")|}] // Version '1.2.3.4' is not valid for platform 'OSX'. Use a version with 2-3 parts for this platform.
    public void SupportedOSPlatformOSX4PartsInvalid() { }

    [SupportedOSPlatform(""Osx1.2"")]
    public void SupportedOsx2PartsValid() { }
}";
            await VerifyAnalyzerCsAsync(csSource,
                VerifyCS.Diagnostic(UseValidPlatformString.InvalidVersion).WithLocation(0).WithArguments("1.2.3.4", "MacOS", "-3"),
                VerifyCS.Diagnostic(UseValidPlatformString.InvalidVersion).WithLocation(1).WithArguments("1.2.3.4", "OSX", "-3"));
        }

        [Fact]
        public async Task APIsWithMultiplePlatformSupportUnsupportAsync()
        {
            var csSource = @"
using System.Runtime.Versioning;

public class Test
{
    [{|#0:SupportedOSPlatform(""MacOS1.2.3.4"")|}] // Version '1.2.3.4' is not valid for platform 'MacOS'. Use a version with 2-3 parts for this platform.
    [SupportedOSPlatform(""Osx2.3"")]
    [SupportedOSPlatform(""Linux"")]
    [[|SupportedOSPlatform(""Browser4.3"")|]] // Browser should not have a version
    public void SupportedOSPlatformMac4PartsInvalid() { }

    [SupportedOSPlatform(""MacOS1.2"")]
    [SupportedOSPlatform(""Osx2.3"")]
    public void SupportedMacOsOsxPartsValid() { }

    [UnsupportedOSPlatform(""osx"")]
    [{|#1:SupportedOSPlatform(""OSX1.2.3.4"")|}] // Version '1.2.3.4' is not valid for platform 'OSX'. Use a version with 2-3 parts for this platform.
    [UnsupportedOSPlatform(""browser"")]
    public void UnsupportedBrowserOSXSupportHasWarning() { }

    [UnsupportedOSPlatform(""browser"")]
    [[|UnsupportedOSPlatform(""Linux1.0"")|]] // Linux should not have a version
    public void UnsupportedOSPlatformOSX4PartsInvalid() { }

    [SupportedOSPlatform(""Osx1.2"")]
    [[|SupportedOSPlatform(""MacOS1.2.3.4"")|]] // Version '1.2.3.4' is not valid for platform 'MacOS'. Use a version with 2-3 parts for this platform.
    public void SupportedOsx2PartValid() { }
}";
            await VerifyAnalyzerCsAsync(csSource,
                VerifyCS.Diagnostic(UseValidPlatformString.InvalidVersion).WithLocation(0).WithArguments("1.2.3.4", "MacOS", "-3"),
                VerifyCS.Diagnostic(UseValidPlatformString.InvalidVersion).WithLocation(1).WithArguments("1.2.3.4", "OSX", "-3"));
        }

        private static async Task VerifyAnalyzerCsAsync(string sourceCode, params DiagnosticResult[] expectedDiagnostics)
        {
            var test = PopulateTestCs(sourceCode);
            test.ExpectedDiagnostics.AddRange(expectedDiagnostics);
            await test.RunAsync();
        }

        private static async Task VerifyAnalyzerCsAsync(string sourceCode, string editorconfigText)
        {
            var test = PopulateTestCs(sourceCode);
            test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", editorconfigText));
            await test.RunAsync();
        }

        private static VerifyCS.Test PopulateTestCs(string sourceCode)
        {
            return new VerifyCS.Test
            {
                TestCode = sourceCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
            };
        }

        private static async Task VerifyAnalyzerAsyncVbAsync(string sourceCode)
        {
            var task = PopulateTestVb(sourceCode);
            await task.RunAsync();
        }

        private static async Task VerifyAnalyzerAsyncVbAsync(string sourceCode, string editorconfigText)
        {
            var test = PopulateTestVb(sourceCode);
            test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", editorconfigText));
            await test.RunAsync();
        }

        private static VerifyVB.Test PopulateTestVb(string sourceCode)
        {
            return new VerifyVB.Test
            {
                TestCode = sourceCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
            };
        }
    }
}