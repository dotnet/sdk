// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using CSharpLanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion;
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
        private const string s_msBuildPlatforms = "build_property._SupportedPlatformList=windows,browser,macOS,maccatalyst, ios, linux;\nbuild_property.TargetFramework=net5.0\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v5.0";

        [Fact(Skip = "TODO need to be fixed: Test for for wrong arguments, not sure how to report the Compiler error diagnostic")]
        public async Task TestOsPlatformAttributesWithNonStringArgumentAsync()
        {
            var csSource = @"
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

public class Test
{
    [[|SupportedOSPlatform(""Linux"", ""Windows"")|]]
    public void MethodWithTwoArguments() { }

    [UnsupportedOSPlatform([|new string[]{""Linux"", ""Windows""}|])]
    public void MethodWithArrayArgument() { }
}";

            await VerifyAnalyzerCSAsync(csSource);
        }

        public static IEnumerable<object[]> Create_DifferentTfms()
        {
            // Pre-.NET 5 TFMS
            yield return new object[] { "build_property.TargetFramework = net472\nbuild_property.TargetFrameworkIdentifier=.NETFramework\nbuild_property.TargetFrameworkVersion=v4.7.2", false };
            yield return new object[] { "build_property.TargetFramework = netcoreapp1.0\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v1.0", false };
            yield return new object[] { "build_property.TargetFramework = dotnet\nbuild_property.TargetFrameworkIdentifier=.NETPlatform\nbuild_property.TargetFrameworkVersion=v5.0", false };
            yield return new object[] { "build_property.TargetFramework = uap10.0\nbuild_property.TargetFrameworkIdentifier=UAP\nbuild_property.TargetFrameworkVersion=v10.0", false };
            yield return new object[] { "build_property.TargetFramework = netstandard2.1\nbuild_property.TargetFrameworkIdentifier=.NETStandard\nbuild_property.TargetFrameworkVersion=v2.1", false };

            // .NET 5+ TFMs with a single major version digit
            yield return new object[] { "build_property.TargetFramework = net5\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v5.0", true };
            yield return new object[] { "build_property.TargetFramework = net5.0\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v5.0", true };
            yield return new object[] { "build_property.TargetFramework = net5.0-windows\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v5.0", true };
            yield return new object[] { "build_property.TargetFramework = net5.0-ios14.0\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v5.0", true };
            yield return new object[] { "build_property.TargetFramework = netcoreapp5\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v5.0", true };

            // .NET 5+ TFMs that have more than one major version digit
            yield return new object[] { "build_property.TargetFramework = net10.0\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v10.0", true };
            yield return new object[] { "build_property.TargetFramework = net11.0\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v11.0", true };

            // Custom TFMs with valid user specified Identifier/Version
            yield return new object[] { "build_property.TargetFramework = nonesense\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v11.0", true };

            // Custom TFMs with invalid user specified Identifier/Version
            yield return new object[] { "build_property.TargetFramework = nonesense\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v5", false };
            yield return new object[] { "build_property.TargetFramework = nonesense\nbuild_property.TargetFrameworkIdentifier=NETCoreApp\nbuild_property.TargetFrameworkVersion=v5.0", false };

            // Custom TFMs with inferred Identifier/Version
            yield return new object[] { "build_property.TargetFramework = nonesense\nbuild_property.TargetFrameworkIdentifier=Unsupported\nbuild_property.TargetFrameworkVersion=v0.0", false };
        }

        [Theory]
        [MemberData(nameof(Create_DifferentTfms))]
        public async Task Net5OrHigherTfmWarns_LowerThanNet5NotWarnAsync(string tfm, bool warn)
        {
            var invocation = warn ? "[|Target.WindowsOnlyMethod()|]" : "Target.WindowsOnlyMethod()";
            var source = @"
using System.Runtime.Versioning;

namespace CallerTargetsBelow5_0
{
    class Caller
    {
        public static void TestWindowsOnlyMethod()
        {
            " + invocation + @";
        }
    }

    class Target
    {
        [SupportedOSPlatform(""windows"")]
        public static void WindowsOnlyMethod() { }
    }
}";
            await VerifyAnalyzerCSAsync(source, tfm);
        }

        public static IEnumerable<object[]> Create_DifferentTfmsWithOption()
        {
            // Pre-.NET 5 TFMS
            yield return new object[] { "build_property.TargetFramework = net472\nbuild_property.TargetFrameworkIdentifier=.NETFramework\nbuild_property.TargetFrameworkVersion=v4.7.2\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=true", true };
            yield return new object[] { "build_property.TargetFramework = net472\nbuild_property.TargetFrameworkIdentifier=.NETFramework\nbuild_property.TargetFrameworkVersion=v4.7.2\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", false };
            yield return new object[] { "build_property.TargetFramework = netcoreapp1.0\n\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v1.0\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=true", true };
            yield return new object[] { "build_property.TargetFramework = netcoreapp1.0\n\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v1.0\ndotnet_code_quality.CA1416.enable_platform_analyzer_on_pre_net5_target=false", false };
            yield return new object[] { "build_property.TargetFramework = dotnet\nbuild_property.TargetFrameworkIdentifier=.NETPlatform\nbuild_property.TargetFrameworkVersion=v5.0\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=true", true };
            yield return new object[] { "build_property.TargetFramework = uap10.0\nbuild_property.TargetFrameworkIdentifier=UAP\nbuild_property.TargetFrameworkVersion=v10.0\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", false };
            yield return new object[] { "build_property.TargetFramework = netstandard2.1\nbuild_property.TargetFrameworkIdentifier=.NETStandard\nbuild_property.TargetFrameworkVersion=v2.1\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=true", true };
            yield return new object[] { "build_property.TargetFramework = netstandard2.1\nbuild_property.TargetFrameworkIdentifier=.NETStandard\nbuild_property.TargetFrameworkVersion=v2.1\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", false };

            // .NET 5+ TFMs with a single major version digit
            yield return new object[] { "build_property.TargetFramework = net5\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v5.0\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", true };
            yield return new object[] { "build_property.TargetFramework = net5.0\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v5.0\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", true };
            yield return new object[] { "build_property.TargetFramework = net5.0-windows\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v5.0\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", true };
            yield return new object[] { "build_property.TargetFramework = net5.0-ios14.0\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v5.0\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", true };
            yield return new object[] { "build_property.TargetFramework = net6.0\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v6.0\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", true };
            yield return new object[] { "build_property.TargetFramework = netcoreapp5\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v5.0\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", true };
        }

        [Theory]
        [MemberData(nameof(Create_DifferentTfmsWithOption))]
        public async Task Net5OrHigherTfmWarns_LowerThanNet5WarnsIfEnabledAsync(string tfmAndOption, bool warn)
        {
            var invocation = warn ? "[|Target.WindowsOnlyMethod()|]" : "Target.WindowsOnlyMethod()";
            var source = @"
using System.Runtime.Versioning;

namespace CallerTargetsBelow5_0
{
    class Caller
    {
        public static void TestWindowsOnlyMethod()
        {
            " + invocation + @";
        }
    }

    class Target
    {
        [SupportedOSPlatform(""windows"")]
        public static void WindowsOnlyMethod() { }
    }
}";
            await VerifyAnalyzerCSAsync(source, tfmAndOption);
        }

        [Fact]
        public async Task ProjectWithPlatformNeutralAssemblyPropertyTestAsync()
        {
            var source = @"
using System.Collections.Generic;
using System.Runtime.Versioning;

[assembly:SupportedOSPlatform(""linux"")] 
public class Test
{
    private string program;

    [SupportedOSPlatform(""windows"")] // Can overwrite parent linux support as cross platform
    public string WindowsOnlyProgram => program;

    [SupportedOSPlatform(""windows"")]
    [SupportedOSPlatform(""ios"")]
    [SupportedOSPlatform(""linux"")]
    public string WindowsIosLinuxOnlyProgram => program; // referencing internal field, should not warn

    [SupportedOSPlatform(""android"")] // Can overwrite parent linux support as cross platform
    [SupportedOSPlatform(""browser"")]
    public string AndroidBrowserOnlyProgram => program; // referencing internal field, should not warn

    [UnsupportedOSPlatform(""linux"")]
    public string UnsupportedLinuxProgram => program;

    void CrossPlatformCallSite()
    {   
        // should warn as WindowsOnlyProgram is windows only
        var a = {|#0:WindowsOnlyProgram|};  // This call site is reachable on: 'linux'. 'Test.WindowsOnlyProgram' is only supported on: 'windows'.
        a = {|#1:UnsupportedLinuxProgram|}; // This call site is reachable on: 'linux'. 'Test.UnsupportedLinuxProgram' is unsupported on: 'linux'.
        a = WindowsIosLinuxOnlyProgram;
        a = {|#2:AndroidBrowserOnlyProgram|}; // This call site is reachable on: 'linux'. 'Test.AndroidBrowserOnlyProgram' is only supported on: 'android', 'browser'.
        List<Test> tests = new List<Test>();
    }

    [SupportedOSPlatform(""linux"")]
    void LinuxOnlyCallsite()
    {   
        var a = {|#3:WindowsOnlyProgram|};  // This call site is reachable on: 'linux'. 'Test.WindowsOnlyProgram' is only supported on: 'windows'.
        a = {|#4:UnsupportedLinuxProgram|}; // This call site is reachable on: 'linux'. 'Test.UnsupportedLinuxProgram' is unsupported on: 'linux'.
        a = WindowsIosLinuxOnlyProgram;
        a = {|#5:AndroidBrowserOnlyProgram|}; //This call site is reachable on: 'linux'. 'Test.AndroidBrowserOnlyProgram' is only supported on: 'android', 'browser'.
        {|#6:BrowserOnlyCallsite()|};  // This call site is reachable on: 'linux'. 'Test.BrowserOnlyCallsite()' is only supported on: 'browser'.

        List<Test> tests = new List<Test>();
        WindowsIosLinuxOnlyCallsite();
    }

    [SupportedOSPlatform(""windows"")]
    [SupportedOSPlatform(""ios"")]
    [SupportedOSPlatform(""linux"")]
    void WindowsIosLinuxOnlyCallsite()
    {   
        var a = [|WindowsOnlyProgram|]; 
        a = [|UnsupportedLinuxProgram|]; // This call site is reachable on: 'linux', 'ios', 'windows'. 'Test.UnsupportedLinuxProgram' is unsupported on: 'linux'.
        a = WindowsIosLinuxOnlyProgram; 
        a = [|AndroidBrowserOnlyProgram|]; // This call site is reachable on: 'linux', 'ios', 'windows'. 'Test.AndroidBrowserOnlyProgram' is only supported on: 'android', 'browser'.

        List<Test> tests = new List<Test>();
    }

    [SupportedOSPlatform(""browser"")] // causes emtpy set, not warn inside for any reference
    void BrowserOnlyCallsite()
    {   
        var a = WindowsOnlyProgram; 
        a = UnsupportedLinuxProgram; 
        a = WindowsIosLinuxOnlyProgram; 
        a = AndroidBrowserOnlyProgram;
    }
}";
            await VerifyAnalyzerCSAsync(source, "build_property.PlatformNeutralAssembly = true\nbuild_property.TargetFramework=net5.0\nbuild_property.TargetFrameworkIdentifier=.NETCoreApp\nbuild_property.TargetFrameworkVersion=v5.0",
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(0).WithArguments("Test.WindowsOnlyProgram", "'windows'", "'linux'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(1).WithArguments("Test.UnsupportedLinuxProgram", "'linux'", "'linux'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(2).WithArguments("Test.AndroidBrowserOnlyProgram", "'android', 'browser'", "'linux'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(3).WithArguments("Test.WindowsOnlyProgram", "'windows'", "'linux'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(4).WithArguments("Test.UnsupportedLinuxProgram", "'linux'", "'linux'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(5).WithArguments("Test.AndroidBrowserOnlyProgram", "'android', 'browser'", "'linux'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(6).WithArguments("Test.BrowserOnlyCallsite()", "'browser'", "'linux'"));
        }

        [Fact, WorkItem(5963, "https://github.com/dotnet/roslyn-analyzers/pull/5963")]
        public async Task PlatformNeutralAssemblyAndCallSiteHasHigherVersionSupport()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform(""MacCatalyst13.1"")]
public class Test
{
    private static int field1 = 0;

    [SupportedOSPlatform(""ios11.0"")]
    public static void iOS11Method() { field1 = 1; }
}";
            await VerifyAnalyzerCSAsync(csSource, "build_property.PlatformNeutralAssembly = true\nbuild_property.TargetFramework=net5.0");
        }

        [Fact]
        public async Task OnlyThrowsNotSupportedWithOsDependentStringNotWarnsAsync()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;

[SupportedOSPlatform(""Browser"")]
public class Test
{
    private static string s_message = ""Browser not supported"";

    [UnsupportedOSPlatform(""browser"")]
    public static void ThrowPnseWithStringArgument() { throw new PlatformNotSupportedException(s_message); }

    [UnsupportedOSPlatform(""browser"")]
    public static void ThrowNotSupportedWithStringArgument() { throw new NotSupportedException(s_message); }

    [UnsupportedOSPlatform(""browser"")]
    public static void ThrowPnseWithStringAndExceptionArgument() { throw new PlatformNotSupportedException(s_message, new Exception(s_message)); }

    [UnsupportedOSPlatform(""browser"")]
    public static void ThrowPnseDefaultConstructor() { throw new PlatformNotSupportedException(); }
}";
            await VerifyAnalyzerCSAsync(csSource);
        }

        [Fact]
        public async Task ThrowNotSupportedWithOtherOsDependentApiUsageNotWarnsAsync()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;
[assembly: SupportedOSPlatform(""browser"")]

public static class SR
{
    public static string Message {get; set;}
}

[UnsupportedOSPlatform(""browser"")]
public class Test
{
    void ThrowWithStringArgument()
    {
        SR.Message = ""This does not warn because this code is not reachable on any"";
        throw new PlatformNotSupportedException(SR.Message);
    }

    void ThrowNotSupportedWithStringArgument()
    {
        SR.Message = ""This does not warn because this code is not reachable on any"";
        throw new NotSupportedException(SR.Message);
    }
    
    void ThrowWithNoArgument()
    {
        SR.Message = ""This does not warn because this code is not reachable on any"";
        throw new PlatformNotSupportedException();
    }
    
    void ThrowWithStringAndExceptionConstructor()
    {
        SR.Message = ""This does not warn because this code is not reachable on any"";
        throw new PlatformNotSupportedException(SR.Message, new Exception());
    }
    
    void ThrowWithAnotherExceptionUsingResourceString()
    {
        SR.Message = ""This does not warn because this code is not reachable on any"";
        throw new PlatformNotSupportedException(SR.Message, new Exception(SR.Message));
    }
}";
            await VerifyAnalyzerCSAsync(csSource);
        }

        [Fact]
        public async Task ThrowNotSupportedWithOtherStatementAndWithinConditionNotWarnsAsync()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;

[SupportedOSPlatform(""windows"")]
public static class Windows
{
    public static string Message {get; set;}
}

[SupportedOSPlatform(""browser"")]
public static class SR
{
    public static string Message {get; set;}
    public static void M1() { }
}

public class Test
{
    void ThrowWithStringConstructor()
    {
        [|SR.M1()|];
        if (!OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(SR.Message);
        }
        SR.M1();

        [|Windows.Message|] = ""Warns supported only on Windows"";
        if (!OperatingSystem.IsWindows())
        {
            throw new NotSupportedException(Windows.Message);
        }
        Windows.Message = ""It is windows!"";
    }

    void ThrowWithOtherConstructorWarnsForInnnerException()
    {
        [|SR.M1()|];
        if (!OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException();
        }
        SR.M1();

        [|Windows.Message|] = ""Warns supported only on Windows"";
        if (!OperatingSystem.IsWindows())
        {
            throw new NotSupportedException(Windows.Message, new Exception([|Windows.Message|]));
        }
    }
}";
            await VerifyAnalyzerCSAsync(csSource);
        }

        [Fact]
        public async Task CreateNotSupportedWithOsDependentStringNotWarnsAsync()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;

[SupportedOSPlatform(""Browser"")]
public class Test
{
    private static string s_message = ""Browser not supported"";

    [UnsupportedOSPlatform(""browser"")]
    public static Exception GetPnseWithStringArgument() { return new PlatformNotSupportedException(s_message); }

    [UnsupportedOSPlatform(""browser"")]
    public static Exception GetNotSupportedWithStringArgument()
    {
        s_message = ""Warns not reachable on Browser"";
        return new NotSupportedException(s_message); 
    }

    [UnsupportedOSPlatform(""browser"")]
    public static Exception ThrowPnseWithStringWarnsForInnerException()
    { 
        return new PlatformNotSupportedException(s_message, new Exception(s_message));
    }

    [UnsupportedOSPlatform(""browser"")]
    public static Exception ThrowPnseDefaultConstructor() { return new PlatformNotSupportedException(); }
}";
            await VerifyAnalyzerCSAsync(csSource);
        }

        [Fact]
        public async Task ThrowNotSupportedWarnsForNonStringArgumentAsync()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;

[SupportedOSPlatform(""windows"")]
public class WindowsOnlyException : Exception 
{
    public WindowsOnlyException() { }
    public WindowsOnlyException(string message) { }
    public static string Message {get; set;}
}

public class Test
{
    void ThrowWindowsOnlyExceptionWarns()
    {
        [|WindowsOnlyException.Message|] = ""Warns for message and exception"";
        throw [|new WindowsOnlyException([|WindowsOnlyException.Message|])|];
    }

    void ThrowWithWindowsOnlyInnnerExceptionWarns()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new NotSupportedException(WindowsOnlyException.Message, [|new WindowsOnlyException()|]);
        }
    }
}";
            await VerifyAnalyzerCSAsync(csSource);
        }

        [Fact]
        public async Task MethodsWithOsDependentTypeParameterWarnsAsync()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;

[SupportedOSPlatform(""windows"")]
class WindowsOnlyType { }

public class Test
{
    void GenericMethod<T>() { }
    void GenericMethod2<T1, T2>() { }
    void M1()
    {
        [|GenericMethod<WindowsOnlyType>()|];
        [|GenericMethod2<Test, WindowsOnlyType>()|];
        [|GenericMethod<Action<WindowsOnlyType>>()|];
    }
}";
            await VerifyAnalyzerCSAsync(csSource);
        }

        [Fact]
        public async Task ConstructorWithOsDependentTypeParameterWarnsAsync()
        {
            var csSource = @"
using System.Runtime.Versioning;

[SupportedOSPlatform(""windows"")]
class WindowsOnlyType { }

class GenericClass<T> { }

public class Test
{
    void MethodWithGenericParameter(GenericClass<WindowsOnlyType> a) {}

    void M1()
    {
        GenericClass<WindowsOnlyType> obj = [|new GenericClass<WindowsOnlyType>()|];
        MethodWithGenericParameter([|new GenericClass<WindowsOnlyType>()|]);
    }
}";
            await VerifyAnalyzerCSAsync(csSource);
        }

        [Fact]
        public async Task ApiContainingTypeHasOsDependentTypeParameterWarnsAsync()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;

[SupportedOSPlatform(""windows"")]
class WindowsOnlyType { }

class GenericClass<T>
{
    public static void M<V>() { }
    public static void M2() { }
    public static int Field;
    public static int Property {get;}
    public static event EventHandler SampleEvent
    {
        add { }
        remove { }
    }
}

public class Test
{
    public static void WindowsEventHandler(object sender, EventArgs e) { }

    void M1()
    {
        [|GenericClass<WindowsOnlyType>.M<int>()|];
        [|GenericClass<WindowsOnlyType>.M2()|];
        [|GenericClass<Action<WindowsOnlyType>>.M2()|];
        [|GenericClass<WindowsOnlyType>.Field|] = 1;
        var val = [|GenericClass<WindowsOnlyType>.Property|];
        [|GenericClass<WindowsOnlyType>.SampleEvent|] += WindowsEventHandler;
    }
}";
            await VerifyAnalyzerCSAsync(csSource);
        }

        [Fact]
        public async Task AssemblyLevelAttribteWithPropertyEventNotWarnAsync()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform(""windows"")]
namespace WindowsOnlyAssembly
{
    public class Test
    {
        private bool _enabled;
        private int _field = 9;
        public int Property
        {
            get => _field;
            set
            {
                _field = value;
            }
        }
        public void WindowsEventHandler(object sender, EventArgs e) { }
        public event EventHandler SampleEvent
        {
            add { }
            remove { }
        }

        public int TestProperty
        {
            get
            {
                Property = _field;
                return Property;
            }
            set
            {
                SampleEvent += WindowsEventHandler;
                _field = value;
            }
        }

        public bool Enabled
        {
            get
            {
                return _enabled;
            }
            set
            {
                _enabled = value;
            }
        }
    }
}";
            await VerifyAnalyzerCSAsync(csSource);
        }

        [Fact]
        public async Task ApiContainingMultipleNestedTypeParameterAsync()
        {
            var csSource = @"
using System;
using System.Runtime.Versioning;

[SupportedOSPlatform(""windows"")]
class WindowsOnlyType { }

class GenericClass<T>
{
    public static void M<V>() { }
    public static void M2() { }
}

class GenericType<T> { }
class AnotherType<T> { }

public class Test
{
    public static void WindowsEventHandler(object sender, EventArgs e) { }

    void M1()
    {
        [|GenericClass<GenericType<AnotherType<WindowsOnlyType>>>.M<int>()|];
        [|GenericClass<GenericType<AnotherType<int>>>.M<GenericClass<GenericType<AnotherType<WindowsOnlyType>>>>()|];
        [|GenericClass<GenericType<AnotherType<Action<WindowsOnlyType>>>>.M2()|];
        [|GenericClass<GenericType<Action<GenericClass<GenericClass<GenericType<Action<GenericClass<WindowsOnlyType>>>>>>>>.M2()|];
        GenericClass<GenericType<Action<GenericClass<GenericClass<GenericType<Action<GenericClass<int>>>>>>>>.M2();
    }
}";
            await VerifyAnalyzerCSAsync(csSource);
        }

        [Fact]
        public async Task OsDependentMethodsCalledWarnsAsync()
        {
            var csSource = @"
using System.Runtime.Versioning;

public class Test
{
    [SupportedOSPlatform(""Linux"")]
    public void M1()
    {
        [|WindowsOnly()|];  // This call site is reachable on: 'Linux'. 'Test.WindowsOnly()' is only supported on: 'Windows' 10.1.1.1 and later.
        [|Unsupported()|];  // This call site is reachable on: 'Linux' all versions. 'Test.Unsupported()' is unsupported on: 'Linux' 4.1 and later.
    }
    
    [UnsupportedOSPlatform(""Linux4.1"")]
    public void Unsupported() { }

    [SupportedOSPlatform(""Windows10.1.1.1"")]
    public void WindowsOnly() { }
}";
            await VerifyAnalyzerCSAsync(csSource);

            var vbSource = @"
Imports System.Runtime.Versioning

Public Class Test
    <SupportedOSPlatform(""Linux"")>
    Public Sub M1()
        [|WindowsOnly()|]
        [|Unsupported()|]
    End Sub

    <SupportedOSPlatform(""Windows10.1.1.1"")>
    Public Sub WindowsOnly()
    End Sub
    
    <UnsupportedOSPlatform(""Linux4.1"")>
    Public Sub Unsupported()
    End Sub
End Class";
            await VerifyAnalyzerVBAsync(vbSource);
        }

        [Fact]
        public async Task WrongPlatformStringsShouldHandledGracefullyAsync()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    public void M1()
    {
        Windows10();
        Windows1_2_3_4_5();
        [|UnsupportedOSPlatformIosDash4_1()|];
        [|UnsupportedOSPlatformIosStar4_1()|];
        [|SupportedLinu4_1()|];
        UnsupportedOSPlatformWithNullString();
        UnsupportedWithEmptyString();
        [|NotForWindows()|];
        UnattributedFunction();
    }
    public void UnattributedFunction() { }

    [UnsupportedOSPlatform(""Windows"")]
    public void NotForWindows() { }

    [SupportedOSPlatform(""Windows10"")]
    public void Windows10() { }

    [SupportedOSPlatform(""Windows1.2.3.4.5"")]
    public void Windows1_2_3_4_5() { }

    [SupportedOSPlatform(""Ios-4.1"")]
    public void UnsupportedOSPlatformIosDash4_1() { }

    [SupportedOSPlatform(""Ios*4.1"")]
    public void UnsupportedOSPlatformIosStar4_1() { }

    [SupportedOSPlatform(null)]
    public void UnsupportedOSPlatformWithNullString() { }

    [SupportedOSPlatform(""Linu4.1"")]
    public void SupportedLinu4_1() { }

    [UnsupportedOSPlatform("""")]
    public void UnsupportedWithEmptyString() { }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task OsDependentPropertyCalledWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    [SupportedOSPlatform(""Windows10.1.1"")]
    public string WindowsStringProperty { get; set; }
    [UnsupportedOSPlatform(""Linux4.1"")]
    public byte UnsupportedProperty { get; }
    [SupportedOSPlatform(""Linux"")]
    public void M1()
    {
        [|WindowsStringProperty|] = ""Hello"";
        string s = [|WindowsStringProperty|];
        M2([|WindowsStringProperty|]);
        M3([|UnsupportedProperty|]);
    }
    public string M2(string option)
    {
        return option;
    }
    public int M3(int option)
    {
        return option;
    }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact, WorkItem(4071, "https://github.com/dotnet/roslyn-analyzers/issues/4071")]
        public async Task OsDependentPropertyGetterSetterCalledWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    [SupportedOSPlatform(""windows"")]
    public static bool WindowsOnlyProperty
    {   
        get { return true; }
        set { }
    }
    public static bool WindowsOnlyPropertyGetter
    {
        [SupportedOSPlatform(""windows"")]
        get { return true; }
        set { }
    }

    public static bool WindowsOnlyPropertySetter
    {
        get { return true; }
        [SupportedOSPlatform(""windows"")]
        set { }
    }

    public void M1()
    {
        WindowsOnlyPropertyGetter = true;
        var s = [|WindowsOnlyPropertyGetter|];
        [|WindowsOnlyPropertyGetter|] |= true;
        [|WindowsOnlyPropertySetter|] &= false;
        [|WindowsOnlyPropertySetter|] = false;
        s = WindowsOnlyPropertySetter;
        M2([|WindowsOnlyPropertyGetter|]);
        M2(WindowsOnlyPropertySetter);
        var name = nameof(WindowsOnlyPropertyGetter);
        name = nameof(WindowsOnlyPropertySetter);
        name = nameof([|WindowsOnlyProperty|]);
    }
    public bool M2(bool option)
    {
        return option;
    }
}";
            await VerifyAnalyzerCSAsync(source);

            var vbSource = @"
Imports System.Runtime.Versioning

Public Class Test
    <SupportedOSPlatform(""windows"")>
    Public Shared Property WindowsOnlyProperty As Boolean
        Get
            Return True
        End Get
        Set(ByVal value As Boolean)
        End Set
    End Property

    Public Shared Property WindowsOnlyPropertyGetter As Boolean
        <SupportedOSPlatform(""windows"")>
        Get
            Return True
        End Get
        Set(ByVal value As Boolean)
        End Set
    End Property

    Public Shared Property WindowsOnlyPropertySetter As Boolean
        Get
            Return True
        End Get
        <SupportedOSPlatform(""windows"")>
        Set(ByVal value As Boolean)
        End Set
    End Property

    Public Sub M1()
        WindowsOnlyPropertyGetter = True
        Dim s = [|WindowsOnlyPropertyGetter|]
        WindowsOnlyPropertyGetter = [|WindowsOnlyPropertyGetter|] Or True
        [|WindowsOnlyPropertySetter|] = WindowsOnlyPropertySetter And False
        [|WindowsOnlyPropertySetter|] = False
        s = WindowsOnlyPropertySetter
        M2([|WindowsOnlyPropertyGetter|])
        Dim name = NameOf(WindowsOnlyPropertyGetter)
    End Sub

    Public Function M2(ByVal[option] As Boolean) As Boolean
        Return[option]
    End Function
End Class";
            await VerifyAnalyzerVBAsync(vbSource);
        }

        [Theory]
        [MemberData(nameof(Create_AttributeProperty_WithCondtions))]
        public async Task OsDependentPropertyConditionalCheckWarnsAsync(string attribute, string property, string condition, string setter, string getter)
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    [" + attribute + @"(""Windows10.1.1"")]
    public " + property + @" { get; set; }

    public void M1()
    {
        [|" + setter + @";
        var s = [|" + getter + @"|];
        bool check = s " + condition + @";
        M2([|" + getter + @"|]);
    }
    public object M2(object option)
    {
        return option;
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        public static IEnumerable<object[]> Create_AttributeProperty_WithCondtions()
        {
            yield return new object[] { "SupportedOSPlatform", "string StringProperty", " == [|StringProperty|]", @"StringProperty|] = ""Hello""", "StringProperty" };
            yield return new object[] { "UnsupportedOSPlatform", "int UnsupportedProperty", " <= [|UnsupportedProperty|]", "UnsupportedProperty|] = 3", "UnsupportedProperty" };
        }

        [Fact]
        public async Task OsDependentEnumValueCalledWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test2
{
    public void M1()
    {
        PlatformEnum val = [|PlatformEnum.Windows10|];
        M2([|PlatformEnum.Windows10|]);
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
    [SupportedOSPlatform(""windows10.0"")]
    Windows10,
    [SupportedOSPlatform(""linux4.8"")]
    Linux48,
    NoPlatform
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task OsDependentEnumConditionalCheckNotWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test2
{
    public void M1()
    {
        PlatformEnum val = [|PlatformEnum.Windows10|];
        if (val == PlatformEnum.Windows10)
            return;
        M2([|PlatformEnum.Windows10|]);
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
}";
            await VerifyAnalyzerCSAsync(source);

            var vbSource = @"
Imports System.Runtime.Versioning

Public Class Test2
    Public Sub M1()
        Dim val As PlatformEnum = [|PlatformEnum.Windows10|]
        If val = [|PlatformEnum.Windows10|] Then Return
        M2([|PlatformEnum.Windows10|])
        M2([|PlatformEnum.Linux48|])
        M2(PlatformEnum.NoPlatform)
    End Sub

    Public Sub M2(ByVal [option] As PlatformEnum)
    End Sub
End Class

Public Enum PlatformEnum
    <SupportedOSPlatform(""Windows10.0"")>
    Windows10
    <SupportedOSPlatform(""Linux4.8"") >
    Linux48
    NoPlatform
End Enum";
            await VerifyAnalyzerVBAsync(vbSource);
        }

        [Fact]
        public async Task OsDependentFieldCalledWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    [SupportedOSPlatform(""Windows10.1.1.1"")]
    string WindowsStringField;
    [SupportedOSPlatform(""Windows10.1.1.1"")]
    public int WindowsIntField;
    public void M1()
    {
        Test test = new Test();
        [|WindowsStringField|] = ""Hello"";
        string s = [|WindowsStringField|];
        M2([|test.WindowsStringField|]);
        M2([|WindowsStringField|]);
        M3([|WindowsIntField|]);
    }
    public string M2(string option)
    {
        return option;
    }
    public int M3(int option)
    {
        return option;
    }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task OsDependentMethodCalledFromInstanceWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    private B field = new B();
    public void M1()
    {
        [|field.M2()|];
    }
}
public class B
{
    [SupportedOSPlatform(""Windows10.1.1.1"")]
    public void M2() { }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task OsDependentMethodCalledFromOtherNsInstanceWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;
using Ns;

public class Test
{
    private B field = new B();
    public void M1()
    {
        [|field.M2()|];
    }
}

namespace Ns
{
    public class B
    {
        [SupportedOSPlatform(""Windows10.1.1.1"")]
        public void M2() { }
    }
}";
            await VerifyAnalyzerCSAsync(source);

            var vbSource = @"
Imports System.Runtime.Versioning
Imports Ns

Public Class Test
    Private field As B = New B()

    Public Sub M1()
        [|field.M2()|]
    End Sub
End Class

Namespace Ns
    Public Class B
        <SupportedOSPlatform(""Windows10.1.1.1"")>
        Public Sub M2()
        End Sub
    End Class
End Namespace";
            await VerifyAnalyzerVBAsync(vbSource);
        }

        [Fact]
        public async Task OsDependentConstructorOfClassUsedCalledWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    public void M1()
    {
        C instance = [|new C()|];
        instance.M2();
    }
}

public class C
{
    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public C() { }

    public void M2() { }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task ConstructorAndMethodOfOsDependentClassCalledWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    public void M1()
    {
        OsDependentClass odc = [|new OsDependentClass()|];
        [|odc.M2()|];
    }
}
[SupportedOSPlatform(""Windows10.1.2.3"")]
public class OsDependentClass
{
    public void M2() { }
}";
            await VerifyAnalyzerCSAsync(source);

            var vbSource = @"
Imports System.Runtime.Versioning

Public Class Test
    Public Sub M1()
        Dim odc As OsDependentClass = [|New OsDependentClass()|]
        [|odc.M2()|]
    End Sub
End Class

<SupportedOSPlatform(""Windows10.1.2.3"")>
Public Class OsDependentClass
    Public Sub M2()
    End Sub
End Class
";
            await VerifyAnalyzerVBAsync(vbSource);
        }

        [Fact]
        public async Task LocalFunctionCallsOsDependentMemberWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    public void M1()
    {
        void Test()
        {
            [|M2()|];
        }
        Test();
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LocalGuardedFunctionCallsOsDependentMemberNotWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    [SupportedOSPlatform(""Windows10.2"")]
    public void M1()
    {
        void Test()
        {
            M2();
        }
        Test();
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LocalFunctionEscapedCallsOsDependentMemberWarnsAsync()
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
            [|M2()|];
        }

        M3(Test);
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }

    public void M3(Action a) { a(); }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LocalFunctionUnusedCallsOsDependentMemberWarnsAsync()
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
            [|M2()|];
        }
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LambdaCallsOsDependentMemberWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class Test
{
    public void M1()
    {
        void Test() => [|M2()|];
        Test();

        Action action = () =>
        {
            [|M2()|];
        };
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task AttributedLambdaCallsOsDependentMemberNotWarnAsync()
        {
            var source = @"
using System.Runtime.Versioning;
using System;

public class C
{
    [SupportedOSPlatform(""Windows10.13"")]
    public void M1()
    {
        void Test() => M2();
        Test();

        Action action = () =>
        {
            M2();
        };
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMemberWarnsAsync()
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
            [|M2()|];
        };

        M3(a);
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }

    public void M3(Action a) { a(); }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task LambdaUnusedCallsOsDependentMemberWarnsAsync()
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
            [|M2()|];
        };
    }

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void M2() { }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task OsDependentEventAccessedWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    public delegate void Del();

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public event Del SampleEvent;

    public void M1()
    {
        [|SampleEvent|] += M3;
        M2();
    }

    public void M2()
    {
        [|SampleEvent|]?.Invoke();
    }

    public void M3() { }
}";
            await VerifyAnalyzerCSAsync(source);

            var vbSource = @"
Imports System.Runtime.Versioning

Public Class Test
    Public Delegate Sub Del()
    <SupportedOSPlatform(""Windows10.1.2.3"")>
    Public Event SampleEvent As Del

    Public Sub M1()
        AddHandler [|SampleEvent|], AddressOf M3
        M2()
    End Sub

    Public Sub M2()
        RaiseEvent  [|SampleEvent|]
    End Sub

    Public Sub M3()
    End Sub
End Class";
            await VerifyAnalyzerVBAsync(vbSource);
        }

        [Fact]
        public async Task EventOfOsDependentTypeAccessedWarnsAsync()
        {
            var source = @"
using System;
using System.Runtime.Versioning;
[SupportedOSPlatform(""windows"")]
public class Test
{
    public static event EventHandler WindowsOnlyEvent
    {
        add { }
        remove { }
    }
}
public class C
{
    public static void WindowsEventHandler(object sender, EventArgs e) { }
    public void M1()
    {
        [|Test.WindowsOnlyEvent|] += WindowsEventHandler;
        [|Test.WindowsOnlyEvent|] -= WindowsEventHandler;
    }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task OsDependentEventAddRemoveAccessedWarnsAsync()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

public class Test
{
    [SupportedOSPlatform(""windows"")]
    public static event EventHandler WindowsOnlyEvent
    {
        add { }
        remove { }
    }

    public static event EventHandler WindowsOnlyEventAdd
    {
        [SupportedOSPlatform(""windows"")]
        add { }
        remove { }
    }

    public static event EventHandler WindowsOnlyEventRemove
    {
        add { }
        [SupportedOSPlatform(""windows"")]
        remove { }
    }

    public static void WindowsEventHandler(object sender, EventArgs e) { }

    public void M1()
    {
        [|WindowsOnlyEvent|] += WindowsEventHandler;
        [|WindowsOnlyEventAdd|] += WindowsEventHandler;
        WindowsOnlyEventRemove += WindowsEventHandler;

        [|WindowsOnlyEvent|] -= WindowsEventHandler;
        WindowsOnlyEventAdd -= WindowsEventHandler;
        [|WindowsOnlyEventRemove|] -= WindowsEventHandler;
    }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task OsDependentMethodAssignedToDelegateWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    public delegate void Del(); // The attribute not supported on delegates, so no tests for that

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void DelegateMethod() { }

    public void M1()
    {
        Del handler = [|DelegateMethod|];
        handler();
    }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact, WorkItem(4168, "https://github.com/dotnet/roslyn-analyzers/issues/4168")]
        public async Task UnsupportedShouldNotSuppressSupportedWithSameVersionAsync()
        {
            var source = @"
using System.Runtime.Versioning;
static class Program
{
    public static void Main()
    {
        [|UnsupportedOnBrowserType.SupportedOnWindowsIosTvos()|]; // This call site is reachable on all platforms. 'UnsupportedOnBrowserType.SupportedOnWindowsIosTvos()' is only supported on: 'ios', 'tvos', 'windows'.
        [|UnsupportedOnBrowserType.SupportedOnTvos4()|];
        UnsupportedOnBrowserType.SupportedOnBrowser();
    }
}
[UnsupportedOSPlatform(""browser"")]
class UnsupportedOnBrowserType
{
    [SupportedOSPlatform(""windows"")]
    [SupportedOSPlatform(""ios"")]
    [SupportedOSPlatform(""tvos"")] 
    public static void SupportedOnWindowsIosTvos() {}
    [SupportedOSPlatform(""browser2.0"")] 
    public static void SupportedOnBrowser() {}
    [SupportedOSPlatform(""tvos4.0"")]    
    public static void SupportedOnTvos4() {}
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact, WorkItem(4168, "https://github.com/dotnet/roslyn-analyzers/issues/4168")]
        public async Task CallSitesUnsupportedShouldNotSuppressSupportedWithSameVersionAsync()
        {
            var source = @"
using System.Runtime.Versioning;

[UnsupportedOSPlatform(""browser"")]
static class Program
{
    public static void Main()
    {
        [|UnsupportedOnBrowserType.SupportedOnWindowsIosTvos()|]; // This call site is reachable on all platforms. 'UnsupportedOnBrowserType.SupportedOnWindowsIosTvos()' is only supported on: 'ios', 'tvos', 'windows'.
        [|UnsupportedOnBrowserType.SupportedOnTvos4()|];
        UnsupportedOnBrowserType.SupportedOnBrowser(); // child support ignored (should not extend)
        UnsupportedOnBrowserType.UnsupportedOnBrowser();
    }
}

[SupportedOSPlatform(""browser"")]
static class Program2
{
    public static void Main()
    {
        [|UnsupportedOnBrowserType.SupportedOnWindowsIosTvos()|]; // This call site is reachable on: 'browser'. 'UnsupportedOnBrowserType.SupportedOnWindowsIosTvos()' is only supported on: 'ios', 'tvos', 'windows'.
        [|UnsupportedOnBrowserType.SupportedOnTvos4()|];  // This call site is reachable on: 'browser'. 'UnsupportedOnBrowserType.SupportedOnTvos4()' is unsupported on: 'browser'.
        [|UnsupportedOnBrowserType.SupportedOnBrowser()|];    // This call site is reachable on: 'browser'. 'UnsupportedOnBrowserType.SupportedOnTvos4()' is unsupported on: 'browser'.
        [|UnsupportedOnBrowserType.UnsupportedOnBrowser()|];  // This call site is reachable on: 'browser'. 'UnsupportedOnBrowserType.SupportedOnTvos4()' is unsupported on: 'browser'.
    }
}

[UnsupportedOSPlatform(""browser"")]
[SupportedOSPlatform(""windows"")]
static class Program3
{
    public static void Main()
    {
        UnsupportedOnBrowserType.SupportedOnWindowsIosTvos(); // No diagnostics expected
        [|UnsupportedOnBrowserType.SupportedOnTvos4()|]; // This call site is reachable on: 'windows'. 'UnsupportedOnBrowserType.SupportedOnTvos4()' is only supported on: 'tvos' 4.0 and later.
        UnsupportedOnBrowserType.SupportedOnBrowser();
        UnsupportedOnBrowserType.UnsupportedOnBrowser();
    }
}

[UnsupportedOSPlatform(""browser"")]
class UnsupportedOnBrowserType
{
    [SupportedOSPlatform(""windows"")]
    [SupportedOSPlatform(""ios"")]
    [SupportedOSPlatform(""tvos"")] 
    public static void SupportedOnWindowsIosTvos() {}
    [SupportedOSPlatform(""browser2.0"")] 
    public static void SupportedOnBrowser() {}
    [UnsupportedOSPlatform(""browser1.0"")] 
    public static void UnsupportedOnBrowser() {}
    [SupportedOSPlatform(""tvos4.0"")]    
    public static void SupportedOnTvos4() {}
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact, WorkItem(4168, "https://github.com/dotnet/roslyn-analyzers/issues/4168")]
        public async Task CallSiteUnsupportedShouldNotSuppressSupportedWithSameVersionAndSameLevelAsync()
        {
            var source = @"
using System.Runtime.Versioning;

[UnsupportedOSPlatform(""browser"")]
static class Program
{
    public static void Main()
    {
        [|UnsupportedOnBrowserSupportedOnWindowType.SupportedOnIosTvos()|]; // One diagnostics expected only for parent, same as below
        [|UnsupportedOnBrowserSupportedOnWindowType.SupportedOnTvos4()|];   // This call site is reachable on all platforms. 'UnsupportedOnBrowserSupportedOnWindowType.SupportedOnTvos4()' is only supported on: 'windows'.
    }
}

[SupportedOSPlatform(""browser"")]
static class Program2
{
    public static void Main()
    {
        [|UnsupportedOnBrowserSupportedOnWindowType.SupportedOnIosTvos()|]; // This call site is reachable on: 'browser'. 'UnsupportedOnBrowserSupportedOnWindowType.SupportedOnIosTvos()' is unsupported on: 'browser'.
        [|UnsupportedOnBrowserSupportedOnWindowType.SupportedOnTvos4()|]; // Same here 
    }
}

[SupportedOSPlatform(""windows"")]
static class Program3
{
    public static void Main()
    {
        UnsupportedOnBrowserSupportedOnWindowType.SupportedOnIosTvos(); // No diagnostics expected
        UnsupportedOnBrowserSupportedOnWindowType.SupportedOnTvos4();
    }
}

[UnsupportedOSPlatform(""browser"")]
[SupportedOSPlatform(""windows"")]
class UnsupportedOnBrowserSupportedOnWindowType
{
    [SupportedOSPlatform(""ios"")]
    [SupportedOSPlatform(""tvos"")]  // these attributes will be ignored
    public static void SupportedOnIosTvos() {}
    [SupportedOSPlatform(""tvos4.0"")]    // same here
    public static void SupportedOnTvos4() {}
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task CallerSupportsSubsetOfTargetAsync()
        {
            var source = @"
using System.Runtime.Versioning;

namespace CallerSupportsSubsetOfTarget
{
    class Caller
    {
        [SupportedOSPlatform(""windows"")]
        public static void Test()
        {
            Target.SupportedOnWindows();
            [|Target.SupportedOnBrowser()|];
            Target.SupportedOnWindowsAndBrowser();
        }
    }

    class Target
    {
        [SupportedOSPlatform(""windows"")]
        public static void SupportedOnWindows() { }

        [SupportedOSPlatform(""browser"")]
        public static void SupportedOnBrowser() { }
        [SupportedOSPlatform(""browser""), SupportedOSPlatform(""windows"")]
        public static void SupportedOnWindowsAndBrowser() { }
    }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task CallerUnsupportsNonSubsetOfTargetSupportAsync()
        {
            var source = @"
using System.Runtime.Versioning;

namespace CallerUnsupportsNonSubsetOfTarget
{
    class Caller
    {
        [UnsupportedOSPlatform(""browser"")]
        public static void TestWithBrowserUnsupported()
        {
            [|Target.UnsupportedOnWindowsUntilWindows11()|];
        }
    }
    class Target
    {
        [UnsupportedOSPlatform(""windows""), SupportedOSPlatform(""windows11.0"")]
        public static void UnsupportedOnWindowsUntilWindows11() { }
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task MacOsSuppressesOsxAndViseVersaAsync()
        {
            var source = @"
using System.Runtime.Versioning;

namespace CallerUnsupportsNonSubsetOfTarget
{
    class Caller
    {
        [SupportedOSPlatform(""MacOS"")]
        public static void TestWithMacOsSupported()
        {
            Target.SupportedOnOSXAndLinux();
            {|#0:Target.SupportedOnOSX14()|}; // This call site is reachable on: 'macOS/OSX' all versions. 'Target.SupportedOnOSX14()' is only supported on: 'macOS/OSX' 14.0 and later.
            Target.SupportedOnMacOs();
        }

        [SupportedOSPlatform(""Linux"")]
        [SupportedOSPlatform(""MacOS"")]
        public static void TestWithMacOsLinuxSupported()
        {
            Target.SupportedOnOSXAndLinux();
            [|Target.SupportedOnMacOs()|]; // This call site is reachable on: 'Linux', 'macOS/OSX'. 'Target.SupportedOnMacOs()' is only supported on: 'macos/OSX'.
        }

        [SupportedOSPlatform(""OSX"")]
        public static void TestWithOsxSupported()
        {
            Target.SupportedOnOSXAndLinux();
            Target.SupportedOnMacOs();
            [|Target.SupportedOnOSX14()|]; // This call site is reachable on: 'macOS/OSX' all versions. 'Target.SupportedOnOSX14()' is only supported on: 'macOS/OSX' 14.0 and later.
        }

        [SupportedOSPlatform(""Linux"")]
        public static void TestWithLinuxSupported()
        {
            Target.SupportedOnOSXAndLinux();
            {|#1:Target.SupportedOnOSX14()|}; // This call site is reachable on: 'Linux'. 'Target.SupportedOnOSX14()' is only supported on: 'macOS/OSX' 14.0 and later.
        }

        public void CrossPlatform()
        {
            [|Target.SupportedOnOSXAndLinux()|]; // This call site is reachable on all platforms. 'Target.SupportedOnOSXAndLinux()' is only supported on: 'macOS/OSX', 'linux'.
            [|Target.SupportedOnOSX14()|]; // This call site is reachable on all platforms. 'Target.SupportedOnOSX14()' is only supported on: 'macOS/OSX' 14.0 and later.
            {|#2:Target.SupportedOnMacOs()|}; // This call site is reachable on all platforms. 'Target.SupportedOnMacOs()' is only supported on: 'macOS/OSX'.
        }
        
        [SupportedOSPlatform(""Browser"")]
        public void TestWithSupportedOnBrowserWarns()
        {
            [|Target.SupportedOnOSXAndLinux()|]; // This call site is reachable on: 'Browser'. 'Target.SupportedOnOSXAndLinux()' is only supported on: 'macOS/OSX', 'linux'.
            [|Target.SupportedOnOSX14()|]; // This call site is reachable on: 'Browser'. 'Target.SupportedOnOSX14()' is only supported on: 'macOS/OSX' 14.0 and later.
        }

        [SupportedOSPlatform(""macos15.1"")]
        public void TestWithSupportedOnMacOs15()
        {
            Target.SupportedOnOSXAndLinux();
            Target.SupportedOnOSX14();
            Target.SupportedOnMacOs();
        }
    }
    class Target
    {
        [SupportedOSPlatform(""osx""), SupportedOSPlatform(""linux"")]
        public static void SupportedOnOSXAndLinux() { }
        [SupportedOSPlatform(""osx14.0"")]
        public static void SupportedOnOSX14() { }
        [SupportedOSPlatform(""macos"")]
        public static void SupportedOnMacOs() { }
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(0).WithArguments("Target.SupportedOnOSX14()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "macOS/OSX", "14.0"),
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "macOS/OSX")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(1).WithArguments("Target.SupportedOnOSX14()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "macOS/OSX", "14.0"), "'Linux'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(2).WithArguments("Target.SupportedOnMacOs()", "'macOS/OSX'"));
        }

        [Fact]
        public async Task MacOsSuppressesOsxAndViseVersa_UnsupportedAsync()
        {
            var source = @"
using System.Runtime.Versioning;

namespace ns
{
    class Caller
    {
        [SupportedOSPlatform(""MacOS"")]
        public static void TestWithMacOsSupported()
        {
            {|#0:Target.UnsupportedOnOSXAndLinux()|}; // This call site is reachable on: 'macOS/OSX'. 'Target.UnsupportedOnOSXAndLinux()' is unsupported on: 'macOS/OSX'.
            [|Target.UnsupportedOnOSX14()|]; // This call site is reachable on: 'macOS/OSX' all versions. 'Target.UnsupportedOnOSX14()' is unsupported on: 'macOS/OSX' 14.0 and later.
        }

        [UnsupportedOSPlatform(""MacOS"")]
        public static void TestWithMacOsLinuxSupported()
        {
            [|Target.UnsupportedOnOSXAndLinux()|]; // This call site is reachable on all platforms. 'Target.UnsupportedOnOSXAndLinux()' is unsupported on: 'linux'.
            Target.UnsupportedOnOSX14();
        }

        [UnsupportedOSPlatform(""OSX"")]
        public static void TestWithOsxSupported()
        {
            [|Target.UnsupportedOnOSXAndLinux()|]; // This call site is reachable on all platforms. 'Target.UnsupportedOnOSXAndLinux()' is unsupported on: 'linux'.
            Target.UnsupportedOnOSX14();
        }

        [SupportedOSPlatform(""Linux"")]
        public static void TestWithLinuxSupported()
        {
            [|Target.UnsupportedOnOSXAndLinux()|]; // This call site is reachable on: 'Linux'. 'Target.UnsupportedOnOSXAndLinux()' is unsupported on: 'linux'.
            Target.UnsupportedOnOSX14();
        }

        public void CrossPlatform()
        {
            [|Target.UnsupportedOnOSXAndLinux()|]; // This call site is reachable on all platforms. 'Target.UnsupportedOnOSXAndLinux()' is unsupported on: 'macOS/OSX'.
            [|Target.UnsupportedOnOSX14()|]; // This call site is reachable on all platforms. 'Target.UnsupportedOnOSX14()' is unsupported on: 'macOS/OSX' 14.0 and later.
        }
        
        [UnsupportedOSPlatform(""macOs13.0"")]
        public void TestWithSupportedOnBrowserWarns()
        {
            [|Target.UnsupportedOnOSXAndLinux()|]; // This call site is reachable on: 'macOS/OSX' 13.0 and before. 'Target.UnsupportedOnOSXAndLinux()' is unsupported on: 'macOS/OSX' all versions.
            Target.UnsupportedOnOSX14();
        }
    }
    class Target
    {
        [UnsupportedOSPlatform(""osx""), UnsupportedOSPlatform(""linux"")]
        public static void UnsupportedOnOSXAndLinux() { }
        [UnsupportedOSPlatform(""osx14.0"")]
        public static void UnsupportedOnOSX14() { }
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0).WithArguments("Target.UnsupportedOnOSXAndLinux()", "'macOS/OSX'", "'macOS/OSX'"));
        }

        [Fact]
        public async Task CallerUnsupportsSubsetOfTargetUsupportedFirstThenSupportsNotWarnAsync()
        {
            var source = @"
using System.Runtime.Versioning;

namespace CallerUnsupportsSubsetOfTarget
{
    class Caller
    {
        [UnsupportedOSPlatform(""windows"")]
        public void TestUnsupportedOnWindows()
        {
            // Call site unsupporting Windows, means the call site supports all other platforms 
            // It is calling into code that was NOT supported only on Windows, but eventually added support,
            // as it was only not supported window supporting it later doesn' matter for call site that is 
            // not supporting windows at all, so it shouldn't raise diagnostic
            TargetUnsupportedOnWindows.FunctionSupportedOnWindows1(); // should not warn
            TargetUnsupportedOnWindows.FunctionSupportedOnWindowsSameVersion();
        }
    }
    [UnsupportedOSPlatform(""windows"")]
    class TargetUnsupportedOnWindows
    {
        [SupportedOSPlatform(""windows1.0"")]
        public static void FunctionSupportedOnWindows1() { }
        
        [SupportedOSPlatform(""windows"")]
        public static void FunctionSupportedOnWindowsSameVersion() { }
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task CallerUnsupportsNonSubsetOfTargetUnsupportedFirstSupportsWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

namespace CallerUnsupportsNonSubsetOfTarget
{
    class Caller
    {
        [UnsupportedOSPlatform(""browser"")]
        public void TestUnsupportedOnWindows()
        {
            [|TargetUnsupportedOnWindows.FunctionSupportedOnWindows1()|];
            [|TargetUnsupportedOnWindows.FunctionSupportedOnWindowsSameVersion()|];
        }
    }
    [UnsupportedOSPlatform(""windows"")]
    class TargetUnsupportedOnWindows
    {
        [SupportedOSPlatform(""windows1.0"")]
        public static void FunctionSupportedOnWindows1() { }
        
        [SupportedOSPlatform(""windows"")]
        public static void FunctionSupportedOnWindowsSameVersion() { }
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task CallerSupportsSupersetOfTarget_AnotherScenarioAsync()
        {
            var source = @"
using System.Runtime.Versioning;

namespace CallerSupportsSubsetOfTarget
{
    class Caller
    {
        [UnsupportedOSPlatform(""browser"")]
        public static void TestWithBrowserUnsupported()
        {
            {|#0:Target.SupportedOnWindows()|}; // This call site is reachable on all platforms. 'Target.SupportedOnWindows()' is only supported on: 'windows'.
            {|#1:Target.SupportedOnBrowser()|}; // This call site is unreachable on: 'browser'. 'Target.SupportedOnBrowser()' is only supported on: 'browser'.
            [|Target.SupportedOnWindowsAndBrowser()|]; // This call site is unreachable on: 'browser'. 'Target.SupportedOnWindowsAndBrowser()' is only supported on: 'browser', 'windows'.
            {|#2:Target.SupportedOnWindowsAndUnsupportedOnBrowser()|}; // This call site is reachable on all platforms. 'Target.SupportedOnWindowsAndUnsupportedOnBrowser()' is only supported on: 'windows'.

            {|#3:Target.UnsupportedOnWindows()|}; // This call site is reachable on all platforms. 'Target.UnsupportedOnWindows()' is unsupported on: 'windows'.
            {|#4:Target.UnsupportedOnWindows11()|};
            Target.UnsupportedOnBrowser();
            {|#5:Target.UnsupportedOnWindowsAndBrowser()|}; // This call site is reachable on all platforms. 'Target.UnsupportedOnWindowsAndBrowser()' is unsupported on: 'windows'.
            {|#6:Target.UnsupportedOnWindowsUntilWindows11()|};
        }
    }

    class Target
    {
        [SupportedOSPlatform(""windows"")]
        public static void SupportedOnWindows() { }

        [SupportedOSPlatform(""browser"")]
        public static void SupportedOnBrowser() { }

        [SupportedOSPlatform(""windows""), SupportedOSPlatform(""browser"")]
        public static void SupportedOnWindowsAndBrowser() { }

        [UnsupportedOSPlatform(""windows"")]
        public static void UnsupportedOnWindows() { }

        [UnsupportedOSPlatform(""windows11.0"")]
        public static void UnsupportedOnWindows11() { }

        [UnsupportedOSPlatform(""browser"")]
        public static void UnsupportedOnBrowser() { }

        [UnsupportedOSPlatform(""windows""), UnsupportedOSPlatform(""browser"")]
        public static void UnsupportedOnWindowsAndBrowser() { }

        [SupportedOSPlatform(""windows""), UnsupportedOSPlatform(""browser"")]
        public static void SupportedOnWindowsAndUnsupportedOnBrowser() { }

        [UnsupportedOSPlatform(""windows""), SupportedOSPlatform(""windows11.0"")]
        public static void UnsupportedOnWindowsUntilWindows11() { }
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(0).WithArguments("Target.SupportedOnWindows()", "'windows'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsUnreachable).WithLocation(1).WithArguments("Target.SupportedOnBrowser()", "'browser'", "'browser'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(2).WithArguments("Target.SupportedOnWindowsAndUnsupportedOnBrowser()", "'windows'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(3).WithArguments("Target.UnsupportedOnWindows()", "'windows'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(4).WithArguments("Target.UnsupportedOnWindows11()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "11.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(5).WithArguments("Target.UnsupportedOnWindowsAndBrowser()", "'windows'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(6).WithArguments("Target.UnsupportedOnWindowsUntilWindows11()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "11.0")));
        }

        [Fact]
        public async Task CallerSupportsSupersetOfTargetAsync()
        {
            var source = @"
using System.Runtime.Versioning;

namespace CallerSupportsSubsetOfTarget
{
    class Caller
    {
        [SupportedOSPlatform(""windows""), SupportedOSPlatform(""browser"")]
        public static void TestWithWindowsAndBrowserSupported()
        {
            [|Target.SupportedOnWindows()|]; // This call site is reachable on: 'windows', 'browser'. 'Target.SupportedOnWindows()' is only supported on: 'windows'.
            [|Target.SupportedOnBrowser()|]; // This call site is reachable on: 'windows', 'browser'. 'Target.SupportedOnBrowser()' is only supported on: 'browser'.
            Target.SupportedOnWindowsAndBrowser();

            [|Target.UnsupportedOnWindows()|]; // This call site is reachable on: 'windows', 'browser'. 'Target.UnsupportedOnWindows()' is unsupported on: 'windows'.
            [|Target.UnsupportedOnBrowser()|]; // This call site is reachable on: 'windows', 'browser'. 'Target.UnsupportedOnBrowser()' is unsupported on: 'browser'.
            [|Target.UnsupportedOnWindowsAndBrowser()|]; // This call site is reachable on: 'windows', 'browser'. 'Target.UnsupportedOnWindowsAndBrowser()' is unsupported on: 'windows', 'browser'.
        }
        [UnsupportedOSPlatform(""browser"")]
        public static void TestWithBrowserUnsupported()
        {
            [|Target.SupportedOnWindows()|]; // This call site is reachable on all platforms. 'Target.SupportedOnWindows()' is only supported on: 'windows'.
            [|Target.SupportedOnBrowser()|]; // This call site is unreachable on: 'browser'. 'Target.SupportedOnBrowser()' is only supported on: 'browser'.
            [|Target.SupportedOnWindowsAndBrowser()|]; // This call site is unreachable on: 'browser'. 'Target.SupportedOnWindowsAndBrowser()' is only supported on: 'browser', 'windows'.

            Target.UnsupportedOnWindows(); // if call site has now support of it and MSbuild list not containg the platform name it will not be warned
            Target.UnsupportedOnBrowser();
            Target.UnsupportedOnWindowsAndBrowser(); // same here
        }
    }

    class Target
    {
        [SupportedOSPlatform(""windows"")]
        public static void SupportedOnWindows() { }

        [SupportedOSPlatform(""browser"")]
        public static void SupportedOnBrowser() { }

        [SupportedOSPlatform(""windows""), SupportedOSPlatform(""browser"")]
        public static void SupportedOnWindowsAndBrowser() { }

        [UnsupportedOSPlatform(""windows"")]
        public static void UnsupportedOnWindows() { }

        [UnsupportedOSPlatform(""browser"")]
        public static void UnsupportedOnBrowser() { }

        [UnsupportedOSPlatform(""windows""), UnsupportedOSPlatform(""browser"")]
        public static void UnsupportedOnWindowsAndBrowser() { }
    }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task AllowDenyListMixedApisTestAsync()
        {
            var source = @"
using System.Runtime.Versioning;

namespace ns
{
    class Caller
    {
        public static void CrossPlatfomr()
        {
            [|Target.UnsupportedBrowserSupportedOnWindowsLinux()|];
            [|Target.SupportedOnWindowsLinuxUnsupportedBrowser()|];
        }

        [SupportedOSPlatform(""windows"")]
        public static void SupportedWindows()
        {
            Target.UnsupportedBrowserSupportedOnWindowsLinux();
            Target.SupportedOnWindowsLinuxUnsupportedBrowser();
        }

        [UnsupportedOSPlatform(""windows"")]
        public static void UnsuportedWindows()
        {
            [|Target.UnsupportedBrowserSupportedOnWindowsLinux()|];
            [|Target.SupportedOnWindowsLinuxUnsupportedBrowser()|];
        }

        [UnsupportedOSPlatform(""browser"")]
        public static void UnsupportedBrowser()
        {
            [|Target.UnsupportedBrowserSupportedOnWindowsLinux()|];
            [|Target.SupportedOnWindowsLinuxUnsupportedBrowser()|];
        }

        [SupportedOSPlatform(""browser"")]
        public static void SupportedBrowser()
        {
            [|Target.UnsupportedBrowserSupportedOnWindowsLinux()|];
            [|Target.SupportedOnWindowsLinuxUnsupportedBrowser()|];
        }
    }

    class Target
    {
        [UnsupportedOSPlatform(""browser"")]
        [SupportedOSPlatform(""windows"")]
        [SupportedOSPlatform(""Linux"")]
        public static void UnsupportedBrowserSupportedOnWindowsLinux() { }

        [SupportedOSPlatform(""windows"")]
        [SupportedOSPlatform(""Linux"")]
        [UnsupportedOSPlatform(""browser"")]
        public static void SupportedOnWindowsLinuxUnsupportedBrowser() { }
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task UnsupportedSamePlatformMustSuppressSupportedAsync()
        {
            var source = @"
using System.Runtime.Versioning;

static class Program
{
    public static void Main()
    {
        [|Some.Api1()|]; // This call site is reachable on all platforms. 'Some.Api1()' is only supported on: 'ios' 10.0 and later, 'tvos' 4.0 and later.
        [|Some.Api2()|]; // This call site is reachable on all platforms. 'Some.Api2()' is only supported on: 'ios' 10.0 and later.
    }
}

[SupportedOSPlatform(""ios10.0"")]
[SupportedOSPlatform(""tvos4.0"")]
class Some
{
    public static void Api1() {}

    [UnsupportedOSPlatform(""tvos"")] // Not ignored, suppresses parent
    public static void Api2() {}
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task MergePlatformAttributesCrushTest()
        {
            var source = @"
using System.Runtime.Versioning;

[SupportedOSPlatform(""ios10.0"")]
static class Program
{
    public static void Main()
    {
        [|Some.Api1()|]; // This call site is reachable on all platforms. 'Some.Api1()' is only supported on: 'ios' 14.0 and later, 'maccatalyst' 14.0 and later
    }
}

[SupportedOSPlatform(""ios10.0"")]
[SupportedOSPlatform(""tvos10.0"")]
[SupportedOSPlatform(""macos10.14"")]
[SupportedOSPlatform(""maccatalyst13.1"")]
[UnsupportedOSPlatform(""watchos"")]
class Some
{
    [UnsupportedOSPlatform(""watchos"")]
    [UnsupportedOSPlatform(""tvos"")]
    [UnsupportedOSPlatform(""macos"")]
    [SupportedOSPlatform(""ios14.0"")]
    public static void Api1() {}
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task PlatformOverridesAsync()
        {
            var source = @"
using System.Runtime.Versioning;

namespace PlatformCompatDemo.Bugs
{
    class Caller
    {
        [SupportedOSPlatform(""windows"")]
        public void TestSupportedOnWindows()
        {
            [|TargetSupportedOnWindows.FunctionUnsupportedOnWindows()|]; // This call site is reachable on: 'windows'. 'TargetSupportedOnWindows.FunctionUnsupportedOnWindows()' is unsupported on: 'windows'.
            TargetSupportedOnWindows.FunctionUnsupportedOnBrowser();     // browser unsupport not related so ignored

            [|TargetUnsupportedOnWindows.FunctionSupportedOnWindows()|]; // This call site is reachable on: 'windows'. 'TargetUnsupportedOnWindows.FunctionSupportedOnWindows()' is unsupported on: 'windows'.
            [|TargetUnsupportedOnWindows.FunctionSupportedOnBrowser()|]; // should warn for unsupported windows and browser support                                   
        }

        [UnsupportedOSPlatform(""windows"")]
        public void TestUnsupportedOnWindows()
        {
            TargetSupportedOnWindows.FunctionUnsupportedOnWindows();
            [|TargetSupportedOnWindows.FunctionUnsupportedOnBrowser()|];  // should warn supported on windows ignore unsupported on browser as this is allow list
                                                                          // This call site is unreachable on: 'windows'. 'TargetSupportedOnWindows.FunctionUnsupportedOnBrowser()' is only supported on: 'windows'.
            [|TargetUnsupportedOnWindows.FunctionSupportedOnBrowser()|];  // This call site is reachable on all platforms. 'TargetUnsupportedOnWindows.FunctionSupportedOnBrowser()' is only supported on: 'browser'. 
            TargetUnsupportedOnWindows.FunctionSupportedOnWindows();      // it's unsupporting Windows at the call site, so it should warn for windows support on the API
        }                                   
    }

    [SupportedOSPlatform(""windows"")]
    class TargetSupportedOnWindows
    {
        [UnsupportedOSPlatform(""windows"")]  // Not  Ignored
        public static void FunctionUnsupportedOnWindows() { }

        [UnsupportedOSPlatform(""browser"")]  // Will be ignored ignored
        public static void FunctionUnsupportedOnBrowser() { }
    }

    [UnsupportedOSPlatform(""windows"")]
    class TargetUnsupportedOnWindows
    {
        [SupportedOSPlatform(""windows"")] // will be ignored
        public static void FunctionSupportedOnWindows() { }

        [SupportedOSPlatform(""browser"")]
        public static void FunctionSupportedOnBrowser() { }
    }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task ChildUnsupportedMustParentSupportedPlatformMustNotIgnoredAsync()
        {
            var source = @"
using System.Runtime.Versioning;
[assembly:SupportedOSPlatform(""browser"")]
namespace PlatformCompatDemo
{
    static class Program
    {
        public static void Main()
        {
            [|CrossPlatformApis.DoesNotWorkOnBrowser()|];
            CrossPlatformApis.NormalFunction();
            var nonBrowser = new NonLinuxApis();
        }
    }

    public class CrossPlatformApis
    {
        [UnsupportedOSPlatform(""browser"")]
        public static void DoesNotWorkOnBrowser() { }
        public static void NormalFunction() { }
    }

    [UnsupportedOSPlatform(""linux"")] // must be ignored
    public class NonLinuxApis { }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task SupportedMustSuppressUnsupportedAssemblyAttributeAsync()
        {
            var source = @"
using System.Runtime.Versioning;
[assembly:UnsupportedOSPlatform(""browser"")]

namespace PlatformCompatDemo
{
    static class Program
    {
        public static void Main()
        {
            [|CrossPlatformApis.WindowsApi()|];
            var nonBrowser = new BrowserApis();
        }
    }

    public class CrossPlatformApis
    {
        [SupportedOSPlatform(""windows"")]
        public static void WindowsApi() { }
    }

    [SupportedOSPlatform(""browser"")]
    public class BrowserApis { }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task UsingUnsupportedApiWithinAllowListShouldWarnAsync()
        {
            var source = @"
using System.Runtime.Versioning;
[assembly: SupportedOSPlatform(""windows"")]

static class Program
{
    public static void Main()
    {
        new SomeWindowsSpecific();
        [|SomeWindowsSpecific.NotForWindows()|];
    }
}

class SomeWindowsSpecific
{
    [UnsupportedOSPlatform(""windows"")] // This will not be ignored
    public static void NotForWindows() { }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task UsingVersionedApiFromAllowListAssemblyNotIgnoredAsync()
        {
            var source = @"
using System.Runtime.Versioning;
[assembly: SupportedOSPlatform(""windows"")]

static class Program
{
    public static void Main()
    {
        [|Some.Windows10SpecificApi()|];
        WindowsSpecificApi();
    }

    public static void WindowsSpecificApi() { }
}

[SupportedOSPlatform(""windows10.0"")] // This attribute will not be ignored
static class Some
{
    public static void Windows10SpecificApi() { }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task ReintroducingHigherApiSupport_WarnAsync()
        {
            var source = @"
using System.Runtime.Versioning;
[assembly: SupportedOSPlatform(""windows10.0"")]

static class Program
{
    public static void Main()
    {
        [|Some.WindowsSpecificApi11()|];
        Some.WindowsSpecificApi1();
    }
}

static class Some
{
    [SupportedOSPlatform(""windows11.0"")]
    public static void WindowsSpecificApi11() { }

    [SupportedOSPlatform(""windows1.0"")]
    public static void WindowsSpecificApi1() { }
}";
            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task MethodOfOsDependentAssemblyCalledWithoutSuppressionWarnsAsync()
        {
            var source = @"
using System.Threading;

namespace ns
{
    public class Test
    {
        public void M1()
        {
            var foo = {|#0:new Overlapped()|};
            var result = {|#1:foo.AsyncResult|};
        }
    }
}
";
            await VerifyAnalyzerCSAsync(source, VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(0).WithArguments("Overlapped", "'windows'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(1).WithArguments("Overlapped.AsyncResult", "'windows'"));
        }

        public static IEnumerable<object[]> SupportedOsAttributeTestData()
        {
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "10.1.2.3", false };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "10.1.3.3", false };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "10.1.3", false };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "11.0", false };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "10.2.2.0", false };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "10.1.1.3", true };
            yield return new object[] { "Windows", "10.1.2.3", "WINDOWS", "11.1.1.3", false };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "10.1.1.4", true };
            yield return new object[] { "MACOS", "10.1.2.3", "macos", "10.2.2.0", false };
            yield return new object[] { "OSX", "10.1.2.3", "Osx", "11.1.1.0", false };
            yield return new object[] { "Osx", "10.1.2.3", "osx", "10.2", false };
            yield return new object[] { "Windows", "10.1.2.3", "macOS/OSX", "11.1.1.4", true };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "10.0.1.9", true };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "10.1.1.4", true };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "8.2.3.3", true };
        }

        [Theory]
        [MemberData(nameof(SupportedOsAttributeTestData))]
        public async Task MethodOfOsDependentClassSuppressedWithSupportedOsAttributeAsync(string platform, string version, string suppressingPlatform, string suppressingVersion, bool warn)
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    [SupportedOSPlatform(""" + suppressingPlatform + suppressingVersion + @""")]
    public void M1()
    {
        OsDependentClass odc = new OsDependentClass();
        odc.M2();
    }
}

[SupportedOSPlatform(""" + platform + version + @""")]
public class OsDependentClass
{
    public void M2() { }
}";

            if (warn)
            {
                await VerifyAnalyzerCSAsync(source,
                    VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithSpan(9, 32, 9, 54).
                    WithArguments("OsDependentClass", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, platform, version),
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, suppressingPlatform, suppressingVersion)),
                    VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithSpan(10, 9, 10, 17).
                    WithArguments("OsDependentClass.M2()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, platform, version),
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, suppressingPlatform, suppressingVersion)));
            }
            else
            {
                await VerifyAnalyzerCSAsync(source);
            }
        }

        public static IEnumerable<object[]> UnsupportedAttributeTestData()
        {
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "10.1.2.3", false };
            yield return new object[] { "Windows", "10.1.2.3", "Mac", "Os10.1.3.3", true };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "10.1.3.1", true };
            yield return new object[] { "windows", "10.1.2.3", "Windows", "11.1", true };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "10.2.2.0", true };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "10.1.1.3", false };
            yield return new object[] { "windows", "10.1.2.3", "Windows", "10.1.1.3", false };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "10.1.1.4", false };
            yield return new object[] { "Windows", "10.1.2.3", "Osx", "10.1.1.4", true };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "10.1.0.1", false };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "8.2.3.4", false };
        }

        [Theory]
        [MemberData(nameof(UnsupportedAttributeTestData))]
        public async Task MethodOfOsDependentClassSuppressedWithUnsupportedAttributeAsync(string platform,
            string version, string suppressingPlatform, string suppressingVersion, bool warn)
        {
            var source = @"
 using System.Runtime.Versioning;
 
[UnsupportedOSPlatform(""" + suppressingPlatform + suppressingVersion + @""")]
public class Test
{
    public void M1()
    {
        OsDependentClass odc = " + (warn ? "{|#0:new OsDependentClass()|}" : "new OsDependentClass()") + @";
    }
}

[UnsupportedOSPlatform(""" + platform + version + @""")]
public class OsDependentClass { }
";

            if (warn)
            {
                if (platform.Equals(suppressingPlatform, System.StringComparison.OrdinalIgnoreCase))
                {
                    await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms, VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0).
                        WithArguments("OsDependentClass", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, platform, version),
                        GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, suppressingPlatform, suppressingVersion)));
                }
                else
                {
                    await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms, VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(0).
                        WithArguments("OsDependentClass", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, platform, version)));
                }
            }
            else
            {
                await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
            }
        }

        [Fact]
        public async Task UnsupportedNotWarnsForUnrelatedSupportedContextAsync()
        {
            var source = @"
 using System.Runtime.Versioning;
 
[SupportedOSPlatform(""Linux"")]
public class Test
{
    public void M1()
    {
        var obj = new C();
        obj.BrowserMethod();
        {|#0:C.StaticClass.LinuxMethod()|};
        {|#1:C.StaticClass.LinuxVersionedMethod()|};
    }
}

public class C
{
    [UnsupportedOSPlatform(""browser"")]
    public void BrowserMethod() { }
    
    [UnsupportedOSPlatform(""linux4.8"")]
    internal static class StaticClass
    {
        public static void LinuxVersionedMethod() { }
        
        [UnsupportedOSPlatform(""linux"")]
        public static void LinuxMethod() { }
    }
}";

            await VerifyAnalyzerCSAsync(source, VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).
                WithLocation(0).WithArguments("C.StaticClass.LinuxMethod()", "'linux'", "'Linux'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(1).WithArguments("C.StaticClass.LinuxVersionedMethod()",
                GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "linux", "4.8"),
                GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "Linux")));
        }

        [Fact]
        public async Task MultipleAttrbiutesOptionallySupportedListTestAsync()
        {
            var source = @"
 using System.Runtime.Versioning;
 
public class Test
{
    C obj = new C();
    [SupportedOSPlatform(""Linux"")]
    public void DiffferentOsNotWarn()
    {
        obj.UnsupportedSupportedFrom1903To2004();
    }

    [SupportedOSPlatform(""windows"")]
    [UnsupportedOSPlatform(""windows10.0.2000"")]
    public void SupporteWindows()
    {
        // Warns for UnsupportedFirst, Supported
        {|#0:obj.UnsupportedSupportedFrom1903To2004()|}; // This call site is reachable on: 'windows' 10.0.2000 and before. 'C.UnsupportedSupportedFrom1903To2004()' is unsupported on: 'windows' 10.0.1903 and before.
    }

    [UnsupportedOSPlatform(""windows"")]
    [SupportedOSPlatform(""windows10.0.1903"")]
    [UnsupportedOSPlatform(""windows10.0.2003"")]
    public void SameSupportForWindowsNotWarn()
    {
        obj.UnsupportedSupportedFrom1903To2004();
    }
    
    public void AllSupportedWarnForAll()
    {
        {|#1:obj.UnsupportedSupportedFrom1903To2004()|}; // This call site is reachable on all platforms. 'C.UnsupportedSupportedFrom1903To2004()' is supported on: 'windows' from version 10.0.1903 to 10.0.2004.
    }

    [SupportedOSPlatform(""windows10.0.2000"")]
    public void SupportedFromWindows10_0_2000()
    {
        // Should warn for [UnsupportedOSPlatform]
        {|#2:obj.UnsupportedSupportedFrom1903To2004()|}; // This call site is reachable on: 'windows' 10.0.2000 and later. 'C.UnsupportedSupportedFrom1903To2004()' is unsupported on: 'windows' 10.0.2004 and later.
    }

    [SupportedOSPlatform(""windows10.0.1904"")]
    [UnsupportedOSPlatform(""windows10.0.1909"")]
    public void SupportedWindowsFrom10_0_1904_To10_0_1909_NotWarn()
    {
        // Should not warn
        obj.UnsupportedSupportedFrom1903To2004(); //This call site is reachable on: 'windows' from version 10.0.1904 to 10.0.1909. 'C.UnsupportedSupportedFrom1903To2004()' is unsupported on: 'windows' 10.0.2004 and later.
    }
}

public class C
{
    [UnsupportedOSPlatform(""windows"")]
    [SupportedOSPlatform(""windows10.0.1903"")]
    [UnsupportedOSPlatform(""windows10.0.2004"")]
    public void UnsupportedSupportedFrom1903To2004() { }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0).
                WithArguments("C.UnsupportedSupportedFrom1903To2004()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "10.0.1903"),
                GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "10.0.2000")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(1).
                WithArguments("C.UnsupportedSupportedFrom1903To2004()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0.1903", "10.0.2004")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(2).
                WithArguments("C.UnsupportedSupportedFrom1903To2004()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0.2004"),
                GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0.2000")));
        }

        [Fact]
        public async Task MultipleAttrbiutesSupportedOnlyWindowsListTestAsync()
        {
            var source = @"
 using System.Runtime.Versioning;
 
public class Test
{
    C obj = new C();
    [SupportedOSPlatform(""Linux"")]
    public void DiffferentOsWarnsForAll()
    {
        {|#0:obj.WindowsOnlyUnsupportedFrom2004()|}; // This call site is reachable on: 'Linux'. 'C.WindowsOnlyUnsupportedFrom2004()' is only supported on: 'windows' 10.0.2004 and before.
    }

    [SupportedOSPlatform(""windows"")]
    [UnsupportedOSPlatform(""windows10.0.2003"")]
    public void SameSupportForWindowsNotWarn()
    {
        obj.WindowsOnlyUnsupportedFrom2004();
    }
    
    public void AllSupportedWarnForAll()
    {
        {|#1:obj.WindowsOnlyUnsupportedFrom2004()|}; // This call site is reachable on all platforms. 'C.WindowsOnlyUnsupportedFrom2004()' is only supported on: 'windows' 10.0.2004 and before.
    }

    [SupportedOSPlatform(""windows10.0.2000"")]
    public void SupportedFromWindows10_0_2000()
    {
        // Warns for [UnsupportedOSPlatform]
        {|#2:obj.WindowsOnlyUnsupportedFrom2004()|}; //  This call site is reachable on: 'windows' 10.0.2000 and later. 'C.WindowsOnlyUnsupportedFrom2004()' is unsupported on: 'windows' 10.0.2004 and later.
    }
    
    [SupportedOSPlatform(""windows10.0.1904"")]
    [UnsupportedOSPlatform(""windows10.0.1909"")]
    public void SupportedWindowsFrom10_0_1904_To10_0_1909_NotWarn()
    {
        // Should not warn
        obj.WindowsOnlyUnsupportedFrom2004();
    }
}

public class C
{
    [SupportedOSPlatform(""windows"")]
    [UnsupportedOSPlatform(""windows10.0.2004"")]
    public void WindowsOnlyUnsupportedFrom2004() { }
}";
            await VerifyAnalyzerCSAsync(source,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(0).
                WithArguments("C.WindowsOnlyUnsupportedFrom2004()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "10.0.2004"), "'Linux'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(1).
                WithArguments("C.WindowsOnlyUnsupportedFrom2004()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "10.0.2004")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(2).
                WithArguments("C.WindowsOnlyUnsupportedFrom2004()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0.2004"),
                GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0.2000")));
        }

        [Fact]
        public async Task CallSiteSupportedUnupportedNoMsBuildOptionsAsync()
        {
            var source = @"
 using System.Runtime.Versioning;

namespace PlatformCompatDemo.SupportedUnupported 
{
    public class Test
    {
        public static void Supported()
        {
            var supported = new TypeWithoutAttributes();
            {|#0:supported.FunctionSupportedOnWindows()|}; // This call site is reachable on all platforms. 'TypeWithoutAttributes.FunctionSupportedOnWindows()' is only supported on: 'windows'.
            {|#1:supported.FunctionSupportedOnWindows10()|}; // This call site is reachable on all platforms. 'TypeWithoutAttributes.FunctionSupportedOnWindows10()' is only supported on: 'windows' 10.0 and later.
            [|supported.FunctionSupportedOnWindows10AndBrowser()|]; // This call site is reachable on all platforms. 'TypeWithoutAttributes.FunctionSupportedOnWindows10AndBrowser()' is only supported on: 'windows' 10.0 and later, 'browser'.

            var supportedOnWindows = {|#2:new TypeSupportedOnWindows()|}; // This call site is reachable on all platforms. 'TypeSupportedOnWindows' is only supported on: 'windows'.
            {|#3:supportedOnWindows.FunctionSupportedOnBrowser()|}; // browser support ignored
            {|#4:supportedOnWindows.FunctionSupportedOnWindows11AndBrowser()|}; //This call site is reachable on all platforms. 'TypeSupportedOnWindows.FunctionSupportedOnWindows11AndBrowser()' is only supported on: 'windows' 11.0 and later.

            var supportedOnBrowser = {|#5:new TypeSupportedOnBrowser()|};
            [|supportedOnBrowser.FunctionSupportedOnWindows()|]; // This call site is reachable on all platforms. 'TypeSupportedOnBrowser.FunctionSupportedOnWindows()' is only supported on: 'browser'.

            var supportedOnWindows10 = [|new TypeSupportedOnWindows10()|]; // This call site is reachable on all platforms. 'TypeSupportedOnWindows10' is only supported on: 'windows' 10.0 and later.
            {|#6:supportedOnWindows10.FunctionSupportedOnBrowser()|}; // child function support will be ignored

            var supportedOnWindowsAndBrowser = [|new TypeSupportedOnWindowsAndBrowser()|]; // This call site is reachable on all platforms. 'TypeSupportedOnWindowsAndBrowser' is only supported on: 'windows', 'browser'.
            [|supportedOnWindowsAndBrowser.FunctionSupportedOnWindows11()|]; // This call site is reachable on all platforms. 'TypeSupportedOnWindowsAndBrowser.FunctionSupportedOnWindows11()' is only supported on: 'windows' 11.0 and later, 'browser'.
        }

        public static void Unsupported()
        {
            var unsupported = new TypeWithoutAttributes();
            unsupported.FunctionUnsupportedOnWindows();
            unsupported.FunctionUnsupportedOnBrowser();
            unsupported.FunctionUnsupportedOnWindows10();
            unsupported.FunctionUnsupportedOnWindowsAndBrowser();
            unsupported.FunctionUnsupportedOnWindows10AndBrowser();

            var unsupportedOnWindows = new TypeUnsupportedOnWindows();
            unsupportedOnWindows.FunctionUnsupportedOnBrowser();
            unsupportedOnWindows.FunctionUnsupportedOnWindows11();
            unsupportedOnWindows.FunctionUnsupportedOnWindows11AndBrowser();

            var unsupportedOnBrowser = new TypeUnsupportedOnBrowser();
            unsupportedOnBrowser.FunctionUnsupportedOnWindows();
            unsupportedOnBrowser.FunctionUnsupportedOnWindows10();

            var unsupportedOnWindows10 = new TypeUnsupportedOnWindows10();
            unsupportedOnWindows10.FunctionUnsupportedOnBrowser();
            unsupportedOnWindows10.FunctionUnsupportedOnWindows11();
            unsupportedOnWindows10.FunctionUnsupportedOnWindows11AndBrowser();

            var unsupportedOnWindowsAndBrowser = new TypeUnsupportedOnWindowsAndBrowser();
            unsupportedOnWindowsAndBrowser.FunctionUnsupportedOnWindows11();

            var unsupportedOnWindows10AndBrowser = new TypeUnsupportedOnWindows10AndBrowser();
            unsupportedOnWindows10AndBrowser.FunctionUnsupportedOnWindows11();
        }

        public static void UnsupportedCombinations() // no any diagnostics as it is deny list
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
" + TargetTypesForTest;

            await VerifyAnalyzerCSAsync(source,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(0).
                WithArguments("TypeWithoutAttributes.FunctionSupportedOnWindows()", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(1).
                WithArguments("TypeWithoutAttributes.FunctionSupportedOnWindows10()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0", "")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(2).
                WithArguments("TypeSupportedOnWindows", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(3).
                WithArguments("TypeSupportedOnWindows.FunctionSupportedOnBrowser()", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(4).
                WithArguments("TypeSupportedOnWindows.FunctionSupportedOnWindows11AndBrowser()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "11.0", "")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(5).
                WithArguments("TypeSupportedOnBrowser", "'browser'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(6).
                WithArguments("TypeSupportedOnWindows10.FunctionSupportedOnBrowser()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0", "")));
        }

        [Fact]
        public async Task CallSiteSupportedUnupporteWithMsBuildOptionsAsync()
        {
            var source = @"
 using System.Runtime.Versioning;

namespace PlatformCompatDemo.SupportedUnupported 
{
    public class Test
    {
        public static void Unsupported()
        {
            var unsupportedOnBrowser = {|#0:new TypeUnsupportedOnBrowser()|};
            [|unsupportedOnBrowser.FunctionUnsupportedOnWindows()|];   // This call site is reachable on all platforms. 'TypeUnsupportedOnBrowser.FunctionUnsupportedOnWindows()' is unsupported on: 'browser', 'windows'.
            [|unsupportedOnBrowser.FunctionUnsupportedOnWindows10()|]; // This call site is reachable on all platforms. 'TypeUnsupportedOnBrowser.FunctionUnsupportedOnWindows10()' is unsupported on: 'browser', 'windows' 10.0 and later.

            var unsupportedOnWindows10 = {|#1:new TypeUnsupportedOnWindows10()|};
            [|unsupportedOnWindows10.FunctionUnsupportedOnBrowser()|];   // This call site is reachable on all platforms. 'TypeUnsupportedOnWindows10.FunctionUnsupportedOnBrowser()' is unsupported on: 'windows' 10.0 and later, 'browser'.
            {|#2:unsupportedOnWindows10.FunctionUnsupportedOnWindows11()|};
            [|unsupportedOnWindows10.FunctionUnsupportedOnWindows11AndBrowser()|]; // This call site is reachable on all platforms. 'TypeUnsupportedOnWindows10.FunctionUnsupportedOnWindows11AndBrowser()' is unsupported on: 'windows' 10.0 and later, 'browser'.

            var unsupportedOnWindowsAndBrowser = [|new TypeUnsupportedOnWindowsAndBrowser()|]; // This call site is reachable on all platforms. 'TypeUnsupportedOnWindowsAndBrowser' is unsupported on: 'windows', 'browser'.
            [|unsupportedOnWindowsAndBrowser.FunctionUnsupportedOnWindows11()|]; // This call site is reachable on all platforms. 'TypeUnsupportedOnWindowsAndBrowser.FunctionUnsupportedOnWindows11()' is unsupported on: 'windows', 'browser'.

            var unsupportedOnWindows10AndBrowser = [|new TypeUnsupportedOnWindows10AndBrowser()|]; // This call site is reachable on all platforms. 'TypeUnsupportedOnWindows10AndBrowser' is unsupported on: 'windows' 10.0 and later, 'browser'.
            [|unsupportedOnWindows10AndBrowser.FunctionUnsupportedOnWindows11()|];  // This call site is reachable on all platforms. 'TypeUnsupportedOnWindows10AndBrowser.FunctionUnsupportedOnWindows11()' is unsupported on: 'windows' 10.0 and later, 'browser'.
        }

        public static void UnsupportedCombinations()
        {
            var withoutAttributes = new TypeWithoutAttributes();
            {|#3:withoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11()|};
            {|#4:withoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12()|};
            {|#5:withoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12SupportedOnWindows13()|};

            var unsupportedOnWindows = {|#6:new TypeUnsupportedOnWindows()|};
            {|#7:unsupportedOnWindows.FunctionSupportedOnWindows11()|};
            {|#8:unsupportedOnWindows.FunctionSupportedOnWindows11UnsupportedOnWindows12()|};
            {|#9:unsupportedOnWindows.FunctionSupportedOnWindows11UnsupportedOnWindows12SupportedOnWindows13()|};

            var unsupportedOnBrowser = {|#10:new TypeUnsupportedOnBrowser()|};
            {|#11:unsupportedOnBrowser.FunctionSupportedOnBrowser()|};

            var unsupportedOnWindowsSupportedOnWindows11 = {|#12:new TypeUnsupportedOnWindowsSupportedOnWindows11()|};
            {|#13:unsupportedOnWindowsSupportedOnWindows11.FunctionUnsupportedOnWindows12()|};
            {|#14:unsupportedOnWindowsSupportedOnWindows11.FunctionUnsupportedOnWindows12SupportedOnWindows13()|};
        }
    }
}
" + TargetTypesForTest;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(0).
                WithArguments("TypeUnsupportedOnBrowser", "'browser'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(1).
                WithArguments("TypeUnsupportedOnWindows10", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(2).
                WithArguments("TypeUnsupportedOnWindows10.FunctionUnsupportedOnWindows11()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(3).
                WithArguments("TypeWithoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11()",
                GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "11.0"), ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(4).
                WithArguments("TypeWithoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12()",
                GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "11.0", "12.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(5).
                WithArguments("TypeWithoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12SupportedOnWindows13()",
                GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "11.0", "12.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(6).
                WithArguments("TypeUnsupportedOnWindows", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(7).
                WithArguments("TypeUnsupportedOnWindows.FunctionSupportedOnWindows11()", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(8).
                WithArguments("TypeUnsupportedOnWindows.FunctionSupportedOnWindows11UnsupportedOnWindows12()", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(9).
                WithArguments("TypeUnsupportedOnWindows.FunctionSupportedOnWindows11UnsupportedOnWindows12SupportedOnWindows13()", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(10).
                WithArguments("TypeUnsupportedOnBrowser", "'browser'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(11).
                WithArguments("TypeUnsupportedOnBrowser.FunctionSupportedOnBrowser()", "'browser'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(12).
                WithArguments("TypeUnsupportedOnWindowsSupportedOnWindows11",
                GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "11.0"), ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(13).
                WithArguments("TypeUnsupportedOnWindowsSupportedOnWindows11.FunctionUnsupportedOnWindows12()",
                GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "11.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(14).
                WithArguments("TypeUnsupportedOnWindowsSupportedOnWindows11.FunctionUnsupportedOnWindows12SupportedOnWindows13()",
                GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "13.0")));
        }

        [Fact]
        public async Task CallSiteCrossPlatform_CallsSupportedUnsupported_VersionedVersionlessAPIsAsync()
        {
            var source = @"
 using System.Runtime.Versioning;

public class Test
{
    public void CrossPlatformTest()
    {
        {|#0:DenyList.UnsupportedWindows()|}; // This call site is reachable on all platforms. 'DenyList.UnsupportedWindows()' is unsupported on: 'windows'.
        {|#1:DenyList.UnsupportedWindows10()|}; // This call site is reachable on all platforms. 'DenyList.UnsupportedWindows10()' is unsupported on: 'windows' 10.0 and later.
        {|#2:DenyList.UnsupportedSupportedWindows8To10()|}; // This call site is reachable on all platforms. 'DenyList.UnsupportedSupportedWindows8To10()' is supported on: 'windows' from version 8.0 to 10.0.
        {|#3:AllowList.WindowsOnly()|}; // This call site is reachable on all platforms. 'AllowList.WindowsOnly()' is only supported on: 'windows'.
        {|#4:AllowList.Windows10Only()|}; // This call site is reachable on all platforms. 'AllowList.Windows10Only()' is only supported on: 'windows' 10.0 and later.
        {|#5:AllowList.WindowsOnlyUnsupportedFrom10()|}; // This call site is reachable on all platforms. 'AllowList.WindowsOnlyUnsupportedFrom10()' is only supported on: 'windows' 10.0 and before.
        {|#6:AllowList.Windows10OnlyUnsupportedFrom11()|}; // This call site is reachable on all platforms. 'AllowList.Windows10OnlyUnsupportedFrom11()' is only supported on: 'windows' from version 10.0 to 11.0.
    }
}
" + AllowDenyListTestClasses;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(0).WithArguments("DenyList.UnsupportedWindows()", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(1).WithArguments("DenyList.UnsupportedWindows10()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0", "")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(2).WithArguments("DenyList.UnsupportedSupportedWindows8To10()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "8.0", "10.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(3).WithArguments("AllowList.WindowsOnly()", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(4).WithArguments("AllowList.Windows10Only()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0", "")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(5).WithArguments("AllowList.WindowsOnlyUnsupportedFrom10()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "10.0", "")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(6).WithArguments("AllowList.Windows10OnlyUnsupportedFrom11()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "11.0")));
        }

        [Fact]
        public async Task CallSiteWindowsOnly_CallsSupportedUnsupported_VersionedVersionlessAPIsAsync()
        {
            var source = @"
 using System.Runtime.Versioning;
 
public class Test
{   
    [SupportedOSPlatform(""windows"")]
    public void SupportedWindowsTest()
    {
        {|#0:DenyList.UnsupportedWindows()|}; // This call site is reachable on: 'windows'. 'DenyList.UnsupportedWindows()' is unsupported on: 'windows'.
        {|#1:DenyList.UnsupportedWindows10()|}; // This call site is reachable on: 'windows' all versions. 'DenyList.UnsupportedWindows10()' is unsupported on: 'windows' 10.0 and later.
        {|#2:DenyList.UnsupportedSupportedWindows8To10()|}; // This call site is reachable on: 'windows' all versions. 'DenyList.UnsupportedSupportedWindows8To10()' is supported on: 'windows' from version 8.0 to 10.0.
        AllowList.WindowsOnly();
        {|#3:AllowList.Windows10Only()|}; // This call site is reachable on: 'windows' all versions. 'AllowList.Windows10Only()' is only supported on: 'windows' 10.0 and later.
        {|#4:AllowList.WindowsOnlyUnsupportedFrom10()|}; // This call site is reachable on: 'windows' all versions. 'AllowList.WindowsOnlyUnsupportedFrom10()' is unsupported on: 'windows' 10.0 and later.
        {|#5:AllowList.Windows10OnlyUnsupportedFrom11()|}; // This call site is reachable on: 'windows' all versions. 'AllowList.Windows10OnlyUnsupportedFrom11()' is only supported on: 'windows' from version 10.0 to 11.0.
    }
}
" + AllowDenyListTestClasses;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0).WithArguments("DenyList.UnsupportedWindows()", "'windows'", "'windows'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(1).WithArguments("DenyList.UnsupportedWindows10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsReachable).WithLocation(2).WithArguments("DenyList.UnsupportedSupportedWindows8To10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "8.0", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(3).WithArguments("AllowList.Windows10Only()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(4).WithArguments("AllowList.WindowsOnlyUnsupportedFrom10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(5).WithArguments("AllowList.Windows10OnlyUnsupportedFrom11()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "11.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows")));
        }

        [Fact]
        public async Task CallSiteWindows9Only_CallsSupportedUnsupported_VersionedVersionlessAPIsAsync()
        {
            var source = @"
 using System.Runtime.Versioning;
 
public class Test
{
    [SupportedOSPlatform(""windows9.0"")]
    public void SupportedWindows10Test()
    {
        {|#0:DenyList.UnsupportedWindows()|}; // This call site is reachable on: 'windows' 9.0 and later. 'DenyList.UnsupportedWindows()' is unsupported on: 'windows' all versions.
        {|#1:DenyList.UnsupportedWindows10()|}; // This call site is reachable on: 'windows' 9.0 and later. 'DenyList.UnsupportedWindows10()' is unsupported on: 'windows' 10.0 and later.
        {|#2:DenyList.UnsupportedSupportedWindows8To10()|}; // This call site is reachable on: 'windows' 9.0 and later. 'DenyList.UnsupportedSupportedWindows8To10()' is unsupported on: 'windows' 10.0 and later.
        AllowList.WindowsOnly();
        {|#3:AllowList.Windows10Only()|}; // This call site is reachable on: 'windows' 9.0 and later. 'AllowList.Windows10Only()' is only supported on: 'windows' 10.0 and later.
        {|#4:AllowList.WindowsOnlyUnsupportedFrom10()|}; // This call site is reachable on: 'windows' 9.0 and later. 'AllowList.WindowsOnlyUnsupportedFrom10()' is unsupported on: 'windows' 10.0 and later.
        {|#5:AllowList.Windows10OnlyUnsupportedFrom11()|}; // This call site is reachable on: 'windows' 9.0 and later. 'AllowList.Windows10OnlyUnsupportedFrom11()' is only supported on: 'windows' from version 10.0 to 11.0.
    }
}
" + AllowDenyListTestClasses;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0).WithArguments("DenyList.UnsupportedWindows()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "9.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(1).WithArguments("DenyList.UnsupportedWindows10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "9.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(2).WithArguments("DenyList.UnsupportedSupportedWindows8To10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "9.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(3).WithArguments("AllowList.Windows10Only()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "9.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(4).WithArguments("AllowList.WindowsOnlyUnsupportedFrom10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "9.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(5).WithArguments("AllowList.Windows10OnlyUnsupportedFrom11()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "11.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "9.0")));
        }

        [Fact]
        public async Task CallSiteAllowList_CallsSupportedUnsupportedVersionedVersionlessAPIsAsync()
        {
            var source = @"
 using System.Runtime.Versioning;
 
public class Test
{   
    [SupportedOSPlatform(""windows"")]
    [UnsupportedOSPlatform(""windows10.0"")]
    public void SupportedWindowsUnsupported10Test()
    {
        {|#0:DenyList.UnsupportedWindows()|}; // This call site is reachable on: 'windows' 10.0 and before. 'DenyList.UnsupportedWindows()' is unsupported on: 'windows' all versions.
        DenyList.UnsupportedWindows10();
        {|#1:DenyList.UnsupportedSupportedWindows8To10()|}; // This call site is reachable on: 'windows' 10.0 and before. 'DenyList.UnsupportedSupportedWindows8To10()' is unsupported on: 'windows' 8.0 and before.
        AllowList.WindowsOnly(); 
        {|#2:AllowList.Windows10Only()|}; // This call site is reachable on: 'windows' 10.0 and before. 'AllowList.Windows10Only()' is only supported on: 'windows' 10.0 and later.
        AllowList.WindowsOnlyUnsupportedFrom10();
        {|#3:AllowList.Windows10OnlyUnsupportedFrom11()|}; // row16: This call site is reachable on: 'windows' 10.0 and before. 'AllowList.Windows10OnlyUnsupportedFrom11()' is only supported on: 'windows' from version 10.0 to 11.0.
    }

    [SupportedOSPlatform(""windows10.0"")]
    [UnsupportedOSPlatform(""windows11.0"")]
    public void SupportedWindows10Unsupported11Test()
    {
        {|#4:DenyList.UnsupportedWindows()|}; // This call site is reachable on: 'windows' from version 10.0 to 11.0. 'DenyList.UnsupportedWindows()' is unsupported on: 'windows' all versions.
        {|#5:DenyList.UnsupportedWindows10()|}; // This call site is reachable on: 'windows' from version 10.0 to 11.0. 'DenyList.UnsupportedWindows10()' is unsupported on: 'windows' 10.0 and later.
        {|#6:DenyList.UnsupportedSupportedWindows8To10()|}; //  This call site is reachable on: 'windows' from version 10.0 to 11.0. 'DenyList.UnsupportedSupportedWindows8To10()' is unsupported on: 'windows' 10.0 and later.
        AllowList.WindowsOnly(); 
        AllowList.Windows10Only();
        {|#7:AllowList.WindowsOnlyUnsupportedFrom10()|}; // This call site is reachable on: 'windows' from version 10.0 to 11.0. 'AllowList.WindowsOnlyUnsupportedFrom10()' is unsupported on: 'windows' 10.0 and later.
        AllowList.Windows10OnlyUnsupportedFrom11();
    }
}
" + AllowDenyListTestClasses;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0).WithArguments("DenyList.UnsupportedWindows()",
                GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "10.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(1).WithArguments("DenyList.UnsupportedSupportedWindows8To10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "8.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "10.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(2).WithArguments("AllowList.Windows10Only()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "10.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(3).WithArguments("AllowList.Windows10OnlyUnsupportedFrom11()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "11.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "10.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(4).WithArguments("DenyList.UnsupportedWindows()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "11.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(5).WithArguments("DenyList.UnsupportedWindows10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "11.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(6).WithArguments("DenyList.UnsupportedSupportedWindows8To10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "11.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(7).WithArguments("AllowList.WindowsOnlyUnsupportedFrom10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "11.0")));
        }

        [Fact]
        public async Task CallSiteUnsupportedWindows_CallsSupportedUnsupported_VersionedVersionlessAPIsAsync()
        {
            var source = @"
 using System.Runtime.Versioning;
 
public class Test
{   
    [UnsupportedOSPlatform(""windows"")]
    public void UnsupportedWindowsTest()
    {
        DenyList.UnsupportedWindows();
        DenyList.UnsupportedWindows10();
        DenyList.UnsupportedSupportedWindows8To10();
        {|#0:AllowList.WindowsOnly()|}; // This call site is unreachable on: 'windows'. 'AllowList.WindowsOnly()' is only supported on: 'windows'.
        {|#1:AllowList.Windows10Only()|}; // This call site is unreachable on: 'windows' all versions. 'AllowList.Windows10Only()' is only supported on: 'windows' 10.0 and later.
        {|#2:AllowList.WindowsOnlyUnsupportedFrom10()|}; // This call site is unreachable on: 'windows' all versions. 'AllowList.WindowsOnlyUnsupportedFrom10()' is only supported on: 'windows' 10.0 and before.
        {|#3:AllowList.Windows10OnlyUnsupportedFrom11()|}; // This call site is unreachable on: 'windows' all versions. 'AllowList.Windows10OnlyUnsupportedFrom11()' is only supported on: 'windows' from version 10.0 to 11.0.
    }
}
" + AllowDenyListTestClasses;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsUnreachable).WithLocation(0).WithArguments("AllowList.WindowsOnly()", "'windows'", "'windows'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsUnreachable).WithLocation(1).WithArguments("AllowList.Windows10Only()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsUnreachable).WithLocation(2).WithArguments("AllowList.WindowsOnlyUnsupportedFrom10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsUnreachable).WithLocation(3).WithArguments("AllowList.Windows10OnlyUnsupportedFrom11()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "11.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows")));
        }

        [Fact]
        public async Task CallSiteUnsupportedWindows9_CallsSupportedUnsupported_VersionedVersionlessAPIsAsync()
        {
            var source = @"
 using System.Runtime.Versioning;
 
public class Test
{
    [UnsupportedOSPlatform(""windows9.0"")]
    public void UnsupportedWindows9Test()
    {
        {|#0:DenyList.UnsupportedWindows()|}; // This call site is reachable on: 'windows' 9.0 and before. 'DenyList.UnsupportedWindows()' is unsupported on: 'windows' all versions.
        DenyList.UnsupportedWindows10();
        {|#1:DenyList.UnsupportedSupportedWindows8To10()|}; // This call site is reachable on: 'windows' 9.0 and before. 'DenyList.UnsupportedSupportedWindows8To10()' is unsupported on: 'windows' 8.0 and before.
        {|#2:AllowList.WindowsOnly()|}; // This call site is unreachable on: 'windows' 9.0 and later. 'AllowList.WindowsOnly()' is only supported on: 'windows' all versions.
        {|#3:AllowList.Windows10Only()|}; // This call site is reachable on: 'windows' 9.0 and before, and all other platforms. 'AllowList.Windows10Only()' is only supported on: 'windows' 10.0 and later.
        {|#4:AllowList.WindowsOnlyUnsupportedFrom10()|}; // This call site is unreachable on: 'windows' 9.0 and later. 'AllowList.WindowsOnlyUnsupportedFrom10()' is only supported on: 'windows' 10.0 and before.
        {|#5:AllowList.Windows10OnlyUnsupportedFrom11()|}; // This call site is unreachable on: 'windows' 9.0 and later. 'AllowList.Windows10OnlyUnsupportedFrom11()' is only supported on: 'windows' from version 10.0 to 11.0.
    }

    [UnsupportedOSPlatform(""windows11.0"")]
    public void UnsupportedWindows11Test()
    {
        {|#6:DenyList.UnsupportedWindows10()|}; // This call site is reachable on: 'windows' 11.0 and before. 'DenyList.UnsupportedWindows10()' is unsupported on: 'windows' 10.0 and later.
        {|#7:DenyList.UnsupportedSupportedWindows8To10()|}; // This call site is reachable on: 'windows' 11.0 and before. 'DenyList.UnsupportedSupportedWindows8To10()' is supported on: 'windows' from version 8.0 to 10.0.
    }
}
" + AllowDenyListTestClasses;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0).WithArguments("DenyList.UnsupportedWindows()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "9.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(1).WithArguments("DenyList.UnsupportedSupportedWindows8To10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "8.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "9.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsUnreachable).WithLocation(2).WithArguments("AllowList.WindowsOnly()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "9.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(3).WithArguments("AllowList.Windows10Only()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater,
                    "windows", "10.0"), Join(GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "9.0"), MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllPlatforms)),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsUnreachable).WithLocation(4).WithArguments("AllowList.WindowsOnlyUnsupportedFrom10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "9.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsUnreachable).WithLocation(5).WithArguments("AllowList.Windows10OnlyUnsupportedFrom11()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "11.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "9.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(6).WithArguments("DenyList.UnsupportedWindows10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "11.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsReachable).WithLocation(7).WithArguments("DenyList.UnsupportedSupportedWindows8To10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "8.0", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "11.0")));
        }

        [Fact]
        public async Task CallSiteUnsupportedWindowsSupportedFrom9_CallsSupportedUnsupported_VersionedVersionlessAPIsAsync()
        {
            var source = @"
 using System.Runtime.Versioning;
 
public class Test
{   
    [UnsupportedOSPlatform(""windows"")]
    [SupportedOSPlatform(""windows9.0"")]
    public void UnsupportedWindowsTest()
    {
        {|#0:DenyList.UnsupportedWindows()|}; // This call site is reachable on: 'windows' 9.0 and later. 'DenyList.UnsupportedWindows()' is unsupported on: 'windows' all versions.
        {|#1:DenyList.UnsupportedWindows10()|}; // This call site is reachable on: 'windows' 9.0 and later. 'DenyList.UnsupportedWindows10()' is unsupported on: 'windows' 10.0 and later.
        {|#2:DenyList.UnsupportedSupportedWindows8To10()|}; // This call site is reachable on: 'windows' 9.0 and later. 'DenyList.UnsupportedSupportedWindows8To10()' is unsupported on: 'windows' 10.0 and later.
        {|#3:AllowList.WindowsOnly()|}; // This call site is reachable on: 'windows' 9.0 and later, and all other platforms. 'AllowList.WindowsOnly()' is only supported on: 'windows'.
        {|#4:AllowList.Windows10Only()|}; // This call site is reachable on: 'windows' 9.0 and later, and all other platforms. 'AllowList.Windows10Only()' is only supported on: 'windows' 10.0 and later.
        {|#5:AllowList.WindowsOnlyUnsupportedFrom10()|}; // This call site is reachable on: 'windows' 9.0 and later, and all other platforms. 'AllowList.WindowsOnlyUnsupportedFrom10()' is only supported on: 'windows' 10.0 and before.
        {|#6:AllowList.Windows10OnlyUnsupportedFrom11()|}; // This call site is reachable on: 'windows' 9.0 and later, and all other platforms. 'AllowList.Windows10OnlyUnsupportedFrom11()' is only supported on: 'windows' from version 10.0 to 11.0.
    }
}
" + AllowDenyListTestClasses;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0).WithArguments("DenyList.UnsupportedWindows()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "9.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(1).WithArguments("DenyList.UnsupportedWindows10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "9.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(2).WithArguments("DenyList.UnsupportedSupportedWindows8To10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "9.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(3).WithArguments("AllowList.WindowsOnly()", "'windows'", Join(GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "9.0"), MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllPlatforms)),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(4).WithArguments("AllowList.Windows10Only()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater,
                    "windows", "10.0"), Join(GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "9.0"), MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllPlatforms)),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(5).WithArguments("AllowList.WindowsOnlyUnsupportedFrom10()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore,
                    "windows", "10.0"), Join(GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "9.0"), MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllPlatforms)),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(6).WithArguments("AllowList.Windows10OnlyUnsupportedFrom11()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion,
                    "windows", "10.0", "11.0"), Join(GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "9.0"), MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllPlatforms)));
        }

        [Fact]
        public async Task CallSiteUnsupportsWindows9Supported11_CallsSupportedUnsupported_VersionedVersionlessAPIsAsync()
        {
            var source = @"
 using System.Runtime.Versioning;
 
public class Test
{
    [UnsupportedOSPlatform(""windows9.0"")]
    [SupportedOSPlatform(""windows11.0"")]
    public void UnsupportedWindows9Test()
    {
        {|#0:DenyList.UnsupportedWindows()|}; // This call site is reachable on: 'windows' 11.0 and later. 'DenyList.UnsupportedWindows()' is unsupported on: 'windows' all versions.
        {|#1:DenyList.UnsupportedWindows10()|}; // This call site is reachable on: 'windows' 11.0 and later. 'DenyList.UnsupportedWindows10()' is unsupported on: 'windows' 10.0 and later.
        {|#2:DenyList.UnsupportedSupportedWindows8To10()|}; // This call site is reachable on: 'windows' 11.0 and later. 'DenyList.UnsupportedSupportedWindows8To10()' is unsupported on: 'windows' 10.0 and later.
        {|#3:AllowList.WindowsOnly()|}; // This call site is reachable on: 'windows' 11.0 and later, and all other platforms. 'AllowList.WindowsOnly()' is only supported on: 'windows' all versions.
        {|#4:AllowList.Windows10Only()|}; // This call site is reachable on: 'windows' 11.0 and later, and all other platforms. 'AllowList.Windows10Only()' is only supported on: 'windows' 10.0 and later.
        {|#5:AllowList.WindowsOnlyUnsupportedFrom10()|}; // This call site is reachable on: 'windows' 11.0 and later, and all other platforms. 'AllowList.WindowsOnlyUnsupportedFrom10()' is only supported on: 'windows' 10.0 and before.
        {|#6:AllowList.Windows10OnlyUnsupportedFrom11()|}; // This call site is reachable on: 'windows' 11.0 and later, and all other platforms. 'AllowList.Windows10OnlyUnsupportedFrom11()' is only supported on: 'windows' from version 10.0 to 11.0.
    }
}
" + AllowDenyListTestClasses;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0).WithArguments("DenyList.UnsupportedWindows()",
                GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "11.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(1).WithArguments("DenyList.UnsupportedWindows10()",
                GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "11.0")),
                 VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(2).WithArguments("DenyList.UnsupportedSupportedWindows8To10()",
                GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "11.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(3).WithArguments("AllowList.WindowsOnly()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions,
                "windows"), Join(GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "11.0"), MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllPlatforms)),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(4).WithArguments("AllowList.Windows10Only()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater,
                "windows", "10.0"), Join(GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "11.0"), MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllPlatforms)),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(5).WithArguments("AllowList.WindowsOnlyUnsupportedFrom10()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore,
                "windows", "10.0"), Join(GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "11.0"), MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllPlatforms)),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(6).WithArguments("AllowList.Windows10OnlyUnsupportedFrom11()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion,
                "windows", "10.0", "11.0"), Join(GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "11.0"), MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllPlatforms)));
        }

        [Fact]
        public async Task CallSiteUnsupportedWindowsSupportedFrom9To12_CallsSupportedUnsupported_VersionedVersionlessAPIsAsync()
        {
            var source = @"
 using System.Runtime.Versioning;
 
public class Test
{   
    [UnsupportedOSPlatform(""windows"")]
    [SupportedOSPlatform(""windows9.0"")]
    [UnsupportedOSPlatform(""windows12.0"")]
    public void UnsupportedWindowsTest()
    {
        {|#0:DenyList.UnsupportedWindows()|}; // This call site is reachable on: 'windows' from version 9.0 to 12.0. 'DenyList.UnsupportedWindows()' is unsupported on: 'windows' all versions.
        {|#1:DenyList.UnsupportedWindows10()|}; // This call site is reachable on: 'windows' from version 9.0 to 12.0. 'DenyList.UnsupportedWindows10()' is unsupported on: 'windows' 10.0 and later.
        {|#2:DenyList.UnsupportedSupportedWindows8To10()|}; // This call site is reachable on: 'windows' from version 9.0 to 12.0. 'DenyList.UnsupportedSupportedWindows8To10()' is unsupported on: 'windows' 10.0 and later.
        {|#3:AllowList.WindowsOnly()|}; // This call site is reachable on: 'windows' from version 9.0 to 12.0 , and all other platforms. 'AllowList.WindowsOnly()' is only supported on: 'windows' all versions.
        {|#4:AllowList.Windows10Only()|}; // This call site is reachable on: 'windows' from version 9.0 to 12.0, and all other platforms. 'AllowList.Windows10Only()' is only supported on: 'windows' 10.0 and later.
        {|#5:AllowList.WindowsOnlyUnsupportedFrom10()|}; // This call site is reachable on: 'windows' from version 9.0 to 12.0, and all other platforms. 'AllowList.WindowsOnlyUnsupportedFrom10()' is only supported on: 'windows' 10.0 and before
        {|#6:AllowList.Windows10OnlyUnsupportedFrom11()|}; // This call site is reachable on: 'windows' from version 9.0 to 12.0, and all other platforms. 'AllowList.Windows10OnlyUnsupportedFrom11()' is only supported on: 'windows' from version 10.0 to 11.0.
    }
}
" + AllowDenyListTestClasses;

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0).WithArguments("DenyList.UnsupportedWindows()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions, "windows"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "9.0", "12.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(1).WithArguments("DenyList.UnsupportedWindows10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "9.0", "12.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(2).WithArguments("DenyList.UnsupportedSupportedWindows8To10()", GetFormattedString(
                    MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0"), GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "9.0", "12.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(3).WithArguments("AllowList.WindowsOnly()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllVersions,
                    "windows"), Join(GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "9.0", "12.0"), MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllPlatforms)),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(4).WithArguments("AllowList.Windows10Only()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater,
                    "windows", "10.0"), Join(GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "9.0", "12.0"), MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllPlatforms)),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(5).WithArguments("AllowList.WindowsOnlyUnsupportedFrom10()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore,
                    "windows", "10.0"), Join(GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "9.0", "12.0"), MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllPlatforms)),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(6).WithArguments("AllowList.Windows10OnlyUnsupportedFrom11()", GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion,
                    "windows", "10.0", "11.0"), Join(GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "9.0", "12.0"), MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityAllPlatforms)));
        }

        [Fact]
        public async Task ChildParentDifferentSupportedAttributesAsync()
        {
            var source = @"
using System.Runtime.Versioning;

class Caller
{
    public static void Test()
    {
        {|#0:TypeSupportedOnlyOnWindows.ApiWithNoAttrbiute()|};    // This call site is reachable on all platforms. 'TypeSupportedOnlyOnWindows.ApiWithNoAttrbiute()' is only supported on: 'windows'.
        {|#1:TypeSupportedOnlyOnWindows.ApiSupportedOn7()|};       // This call site is reachable on all platforms. 'TypeSupportedOnlyOnWindows.ApiSupportedOn7()' is only supported on: 'windows' 7.0 and later.
        {|#2:TypeSupportedOnlyOnWindows.ApiUnsupportedOn10()|};    // ... 'TypeSupportedOnlyOnWindows.ApiUnsupportedOn10()' is only supported on: 'windows' 10.0 and before.
        {|#3:TypeSupportedOnlyOnWindows.ApiUnsupportedOnWindows()|};   // 'TypeSupportedOnlyOnWindows.ApiUnsupportedOnWindows()' is unsupported on: 'windows'.
        {|#4:TypeSupportedOnlyOnWindows.TypeWindows8.ApiWithNoAttrbiute()|}; // 'TypeSupportedOnlyOnWindows.TypeWindows8.ApiWithNoAttrbiute()' is only supported on: 'windows' 8.0 and later.
        {|#5:TypeSupportedOnlyOnWindows.TypeWindows8.ApiSupportedOn10()|};   // 'TypeSupportedOnlyOnWindows.TypeWindows8.ApiSupportedOn10()' is only supported on: 'windows' 10.0 and later.
        {|#6:TypeSupportedOnlyOnWindows.TypeWindows8.ApiSupportedOn7_10()|}; // 'TypeSupportedOnlyOnWindows.TypeWindows8.ApiSupportedOn7_10()' is only supported on: 'windows' from version 8.0 to 10.0.
    }
}

[SupportedOSPlatform(""windows8.0"")]  // will be ignored
[SupportedOSPlatform(""windows10.0"")] // same
[SupportedOSPlatform(""windows"")]     // only lowest will be a accounted
class TypeSupportedOnlyOnWindows
{
    public static void ApiWithNoAttrbiute() { } // warns with unversioned windows support

    [SupportedOSPlatform(""windows7.0"")]
    public static void ApiSupportedOn7() { } // child narrows version support, so warns for windows 7.0

    [UnsupportedOSPlatform(""windows10.0"")]
    public static void ApiUnsupportedOn10() { }

    [UnsupportedOSPlatform(""windows"")]
    public static void ApiUnsupportedOnWindows() { }
    
    [SupportedOSPlatform(""windows8.0"")]
    internal static class TypeWindows8
    {
        public static void ApiWithNoAttrbiute() { } // warns for windows 8.0 of immediate parent

        [SupportedOSPlatform(""windows11.0"")] 
        [SupportedOSPlatform(""windows10.0"")] // child narrows version support, version 10.0 overrides 8.0
        public static void ApiSupportedOn10() { } 

        [UnsupportedOSPlatform(""windows10.0"")]
        [SupportedOSPlatform(""windows7.0"")]  // child not narrowing parent version support, version 7.0 will be ignored
        [SupportedOSPlatform(""windows11.0"")] // higher than 7.0, ignored
        public static void ApiSupportedOn7_10() { }
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(0).WithArguments("TypeSupportedOnlyOnWindows.ApiWithNoAttrbiute()", "'windows'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(1).WithArguments("TypeSupportedOnlyOnWindows.ApiSupportedOn7()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "7.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(2).WithArguments("TypeSupportedOnlyOnWindows.ApiUnsupportedOn10()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "10.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(3).WithArguments("TypeSupportedOnlyOnWindows.ApiUnsupportedOnWindows()", "'windows'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(4).WithArguments("TypeSupportedOnlyOnWindows.TypeWindows8.ApiWithNoAttrbiute()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "8.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(5).WithArguments("TypeSupportedOnlyOnWindows.TypeWindows8.ApiSupportedOn10()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "10.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(6).WithArguments("TypeSupportedOnlyOnWindows.TypeWindows8.ApiSupportedOn7_10()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "8.0", "10.0")));
        }

        [Fact]
        public async Task ChildParentDifferentUnsupportedAttributesAsync()
        {
            var source = @"
using System.Runtime.Versioning;

class Caller
{
    public static void Test()
    {
        {|#0:TypeUnsupportedWin8.ApiWithNoAttrbiute()|}; // ... 'TypeUnsupportedWin8.ApiWithNoAttrbiute()' is unsupported on: 'windows' 8.0 and later.
        {|#1:TypeUnsupportedWin8.ApiUnsupportedOn9()|};  // 'TypeUnsupportedWin8.ApiUnsupportedOn9()' is unsupported on: 'windows' 8.0 and later.
        {|#2:TypeUnsupportedWin8.ApiSupportedOn10()|};   // 'TypeUnsupportedWin8.ApiSupportedOn10()' is unsupported on: 'windows' 8.0 and later.
        {|#3:TypeUnsupportedWin8.ApiUnsupportedOn7Supported10()|}; // 'TypeUnsupportedWin8.ApiUnsupportedOn7Supported10()' is unsupported on: 'windows' from version 7.0 to 10.0.
        {|#4:TypeUnsupportedWin8.TypeUnsupportedOn7.ApiWithNoAttrbiute()|}; // 'TypeUnsupportedWin8.TypeUnsupportedOn7.ApiWithNoAttrbiute()' is unsupported on: 'windows' 7.0 and later.
        {|#5:TypeUnsupportedWin8.TypeUnsupportedOn7.ApiUnsupportedOn9SupportedOn11()|};  // 'TypeUnsupportedWin8.TypeUnsupportedOn7.ApiUnsupportedOn9SupportedOn11()' is unsupported on: 'windows' 7.0 and later.
        {|#6:TypeUnsupportedWin8.TypeUnsupportedOn7.ApiUnsupportedUntil7()|}; // 'TypeUnsupportedWin8.TypeUnsupportedOn7.ApiUnsupportedUntil7()' is unsupported on: 'windows' 7.0 and before.
        {|#7:TypeUnsupportedWin8.TypeUnsupportedOn7.ApiUnsupportedSupportedOn7_9()|}; // 'TypeUnsupportedWin8.TypeUnsupportedOn7.ApiUnsupportedSupportedOn7_9()' is supported on: 'windows' from version 7.0 to 9.0.
    }
}

[UnsupportedOSPlatform(""windows8.0"")]
[UnsupportedOSPlatform(""windows14.0"")] // ignored as no support in between and lowest version will be kept
class TypeUnsupportedWin8
{
    public static void ApiWithNoAttrbiute() { }

    [UnsupportedOSPlatform(""windows9.0"")] // will be ignored, only lower version can override parent
    public static void ApiUnsupportedOn9() { }

    [SupportedOSPlatform(""windows10.0"")] // will be ignored, should override unsupported first
    public static void ApiSupportedOn10() { } 

    [UnsupportedOSPlatform(""windows7.0"")] // will override parent
    [SupportedOSPlatform(""windows10.0"")] // will work
    public static void ApiUnsupportedOn7Supported10() { } 
    
    [UnsupportedOSPlatform(""windows10.0"")] // ignored
    [UnsupportedOSPlatform(""windows7.0"")]  // will override parent 8
    internal static class TypeUnsupportedOn7
    {
        public static void ApiWithNoAttrbiute() { }

        [UnsupportedOSPlatform(""windows9.0"")] // could not override parent
        [UnsupportedOSPlatform(""windows10.0"")] // ignored
        [SupportedOSPlatform(""windows11.0"")] // ignored as parent has no any support
        public static void ApiUnsupportedOn9SupportedOn11() { }
        
        [SupportedOSPlatform(""windows7.0"")]
        [UnsupportedOSPlatform(""windows"")] // overrides parent version
        [UnsupportedOSPlatform(""windows6.0"")] // ignored
        public static void ApiUnsupportedUntil7() { }

        [SupportedOSPlatform(""windows7.0"")]
        [UnsupportedOSPlatform(""windows"")] // overrides parent version
        [UnsupportedOSPlatform(""windows9.0"")] // will become UnsupportedSecond
        public static void ApiUnsupportedSupportedOn7_9() { }
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(0).WithArguments("TypeUnsupportedWin8.ApiWithNoAttrbiute()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "8.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(1).WithArguments("TypeUnsupportedWin8.ApiUnsupportedOn9()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "8.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(2).WithArguments("TypeUnsupportedWin8.ApiSupportedOn10()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "8.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(3).WithArguments("TypeUnsupportedWin8.ApiUnsupportedOn7Supported10()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "7.0", "10.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(4).WithArguments("TypeUnsupportedWin8.TypeUnsupportedOn7.ApiWithNoAttrbiute()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "7.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(5).WithArguments("TypeUnsupportedWin8.TypeUnsupportedOn7.ApiUnsupportedOn9SupportedOn11()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndLater, "windows", "7.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(6).WithArguments("TypeUnsupportedWin8.TypeUnsupportedOn7.ApiUnsupportedUntil7()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "7.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(7).WithArguments("TypeUnsupportedWin8.TypeUnsupportedOn7.ApiUnsupportedSupportedOn7_9()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "7.0", "9.0")));
        }

        [Fact]
        public async Task ChildParentMultipleSupportedCombinationsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

class Caller
{
    public static void Test()
    {
        {|#0:TypeSupportedWindows8_14.ApiWithNoAttrbiute()|}; // This call site is reachable on all platforms. 'TypeSupportedWindows8_14.ApiWithNoAttrbiute()' is only supported on: 'windows' from version 8.0 to 14.0.
        {|#1:TypeSupportedWindows8_14.UnsupportedOn12()|};    // ... 'TypeSupportedWindows8_14.UnsupportedOn12()' is only supported on: 'windows' from version 8.0 to 12.0.
        {|#2:TypeSupportedWindows8_14.SupportedOn10()|};      // 'TypeSupportedWindows8_14.SupportedOn10()' is only supported on: 'windows' from version 10.0 to 14.0.
        {|#3:TypeSupportedWindows8_14.SupportedOn0_15()|};    // 'TypeSupportedWindows8_14.SupportedOn0_15()' is only supported on: 'windows' from version 8.0 to 14.0.
        {|#4:TypeSupportedWindows8_14.UnsupportedOn0_10()|};  // 'TypeSupportedWindows8_14.UnsupportedOn0_10()' is unsupported on: 'windows' 10.0 and before.
        {|#5:TypeSupportedWindows8_14.UnsupportedOn8_10_13()|}; // 'TypeSupportedWindows8_14.UnsupportedOn8_10_13()' is only supported on: 'windows' from version 10.0 to 13.0.
        {|#6:TypeSupportedWindows8_14.SupportedOn7_10()|};    // 'TypeSupportedWindows8_14.SupportedOn7_10()' is only supported on: 'windows' from version 8.0 to 10.0.
        {|#7:TypeSupportedWindows8_14.SupportedOn9_11()|};    // 'TypeSupportedWindows8_14.SupportedOn9_11()' is only supported on: 'windows' from version 9.0 to 11.0.
        {|#8:TypeSupportedWindows8_14.TypeSupporteOnd9.ApiWithNoAttrbiute()|}; // 'TypeSupportedWindows8_14.TypeSupporteOnd9.ApiWithNoAttrbiute()' is only supported on: 'windows' from version 9.0 to 14.0.
        {|#9:TypeSupportedWindows8_14.TypeSupporteOnd9.SupportedOn10()|}; // 'TypeSupportedWindows8_14.TypeSupporteOnd9.SupportedOn10()' is only supported on: 'windows' from version 10.0 to 14.0.
        {|#10:TypeSupportedWindows8_14.TypeSupporteOnd9.UnsupportedOn12()|}; // 'TypeSupportedWindows8_14.TypeSupporteOnd9.UnsupportedOn12()' is only supported on: 'windows' from version 9.0 to 12.0.
        {|#11:TypeSupportedWindows8_14.TypeSupporteOnd9.SupportedOn0_15()|}; // 'TypeSupportedWindows8_14.TypeSupporteOnd9.SupportedOn0_15()' is only supported on: 'windows' from version 9.0 to 14.0.
        {|#12:TypeSupportedWindows8_14.TypeSupporteOnd9.SupportedOn10_12()|}; // 'TypeSupportedWindows8_14.TypeSupporteOnd9.SupportedOn10_12()' is only supported on: 'windows' from version 10.0 to 12.0.
    }
}

[UnsupportedOSPlatform(""windows14.0"")]
[SupportedOSPlatform(""windows8.0"")] // Allow list because of the lower version
class TypeSupportedWindows8_14
{
    public static void ApiWithNoAttrbiute() { }

    [UnsupportedOSPlatform(""windows12.0"")]
    public static void UnsupportedOn12() { }

    [SupportedOSPlatform(""windows10.0"")]
    public static void SupportedOn10() { }

    [SupportedOSPlatform(""windows"")] // both attributes should be ignored
    [UnsupportedOSPlatform(""windows15.0"")]
    public static void SupportedOn0_15() { }

    [UnsupportedOSPlatform(""windows"")] // Overrides parent support/unsupport
    [SupportedOSPlatform(""windows10.0"")] //
    public static void UnsupportedOn0_10() { }

    [UnsupportedOSPlatform(""windows8.0"")]
    [SupportedOSPlatform(""windows10.0"")]
    [UnsupportedOSPlatform(""windows13.0"")]
    public static void UnsupportedOn8_10_13() { }

    [SupportedOSPlatform(""windows7.0"")]    // ignored
    [UnsupportedOSPlatform(""windows10.0"")] // overrides parent unsupport
    public static void SupportedOn7_10() { }
    
    [SupportedOSPlatform(""windows9.0"")]    // overrides parent support
    [UnsupportedOSPlatform(""windows11.0"")] // overrides parent unsupport
    public static void SupportedOn9_11() { }

    [SupportedOSPlatform(""windows9.0"")] // overrides parent support
    internal static class TypeSupporteOnd9
    {
        public static void ApiWithNoAttrbiute() { }

        [SupportedOSPlatform(""windows10.0"")]
        public static void SupportedOn10() { } // overrides both parent

        [UnsupportedOSPlatform(""windows12.0"")]
        public static void UnsupportedOn12() { }

        [SupportedOSPlatform(""windows"")] // both attributes ignored
        [UnsupportedOSPlatform(""windows15.0"")]
        public static void SupportedOn0_15() { }
        
        [SupportedOSPlatform(""windows10.0"")] // both attributes overrides
        [UnsupportedOSPlatform(""windows12.0"")]
        public static void SupportedOn10_12() { }
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(0).WithArguments("TypeSupportedWindows8_14.ApiWithNoAttrbiute()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "8.0", "14.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(1).WithArguments("TypeSupportedWindows8_14.UnsupportedOn12()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "8.0", "12.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(2).WithArguments("TypeSupportedWindows8_14.SupportedOn10()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "14.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(3).WithArguments("TypeSupportedWindows8_14.SupportedOn0_15()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "8.0", "14.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(4).WithArguments("TypeSupportedWindows8_14.UnsupportedOn0_10()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityVersionAndBefore, "windows", "10.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(5).WithArguments("TypeSupportedWindows8_14.UnsupportedOn8_10_13()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "13.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(6).WithArguments("TypeSupportedWindows8_14.SupportedOn7_10()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "8.0", "10.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(7).WithArguments("TypeSupportedWindows8_14.SupportedOn9_11()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "9.0", "11.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(8).WithArguments("TypeSupportedWindows8_14.TypeSupporteOnd9.ApiWithNoAttrbiute()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "9.0", "14.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(9).WithArguments("TypeSupportedWindows8_14.TypeSupporteOnd9.SupportedOn10()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "14.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(10).WithArguments("TypeSupportedWindows8_14.TypeSupporteOnd9.UnsupportedOn12()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "9.0", "12.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(11).WithArguments("TypeSupportedWindows8_14.TypeSupporteOnd9.SupportedOn0_15()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "9.0", "14.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(12).WithArguments("TypeSupportedWindows8_14.TypeSupporteOnd9.SupportedOn10_12()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "12.0")));
        }

        [Fact]
        public async Task ChildParentMultipleUnsupportedCombinationsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

class Caller
{
    public static void Test()
    {
        {|#0:UnsupportedWindows8Supported9_14.ApiWithNoAttrbiute()|};  // ... 'UnsupportedWindows8Supported9_14.ApiWithNoAttrbiute()' is supported on: 'windows' from version 9.0 to 14.0.
        {|#1:UnsupportedWindows8Supported9_14.UnsupportedOn7()|};      // ...'UnsupportedWindows8Supported9_14.UnsupportedOn7()' is supported on: 'windows' from version 9.0 to 14.0.
        {|#2:UnsupportedWindows8Supported9_14.SupportedOn10Unsupported12()|}; // 'UnsupportedWindows8Supported9_14.SupportedOn10Unsupported12()' is supported on: 'windows' from version 10.0 to 14.0.
        {|#3:UnsupportedWindows8Supported9_14.Unsupported9Supported10.ApiWithNoAttrbiute()|}; // 'UnsupportedWindows8Supported9_14.Unsupported9Supported10.ApiWithNoAttrbiute()' is supported on: 'windows' from version 10.0 to 14.0.
        {|#4:UnsupportedWindows8Supported9_14.Unsupported9Supported10.Unsupported6Supported11_13()|}; // 'UnsupportedWindows8Supported9_14.Unsupported9Supported10.Unsupported6Supported11_13()' is supported on: 'windows' from version 11.0 to 13.0.
    }
}

[UnsupportedOSPlatform(""windows14.0"")]
[UnsupportedOSPlatform(""windows8.0"")]
[SupportedOSPlatform(""windows9.0"")]
class UnsupportedWindows8Supported9_14
{
    public static void ApiWithNoAttrbiute() { }

    [UnsupportedOSPlatform(""windows7.0"")]
    public static void UnsupportedOn7() { }

    [SupportedOSPlatform(""windows10.0"")]
    [UnsupportedOSPlatform(""windows12.0"")]
    public static void SupportedOn10Unsupported12() { }
    
    [UnsupportedOSPlatform(""windows9.0"")] // will be ignored
    [SupportedOSPlatform(""windows10.0"")]  // will override
    internal static class Unsupported9Supported10
    {
        public static void ApiWithNoAttrbiute() { }

        [SupportedOSPlatform(""windows11.0"")]
        [UnsupportedOSPlatform(""windows13.0"")]
        [UnsupportedOSPlatform(""windows6.0"")]
        public static void Unsupported6Supported11_13() { }
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(0).WithArguments("UnsupportedWindows8Supported9_14.ApiWithNoAttrbiute()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "9.0", "14.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(1).WithArguments("UnsupportedWindows8Supported9_14.UnsupportedOn7()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "9.0", "14.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(2).WithArguments("UnsupportedWindows8Supported9_14.SupportedOn10Unsupported12()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "14.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(3).WithArguments("UnsupportedWindows8Supported9_14.Unsupported9Supported10.ApiWithNoAttrbiute()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "14.0")),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(4).WithArguments("UnsupportedWindows8Supported9_14.Unsupported9Supported10.Unsupported6Supported11_13()",
                    GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "11.0", "13.0")));
        }

        [Fact]
        public async Task ChildParentMultiplePlatformAttributesCombinationsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

class Caller
{
    public static void Test()
    {
        {|#0:UnsupportedWindows8_9_12_Ios6_7_14.ApiWithNoAttrbiute()|}; // This call site is reachable on all platforms. 'UnsupportedWindows8_9_12_Ios6_7_14.ApiWithNoAttrbiute()' is supported on: 'windows' from version 9.0 to 12.0, 'ios' from version 7.0 to 14.0.
        {|#1:UnsupportedWindows8_9_12_Ios6_7_14.UnsupportedWindows6_10_Ios_1_5_11.ApiWithNoAttrbiute()|}; // 'UnsupportedWindows8_9_12_Ios6_7_14.UnsupportedWindows6_10_Ios_1_5_11.ApiWithNoAttrbiute()' is supported on: 'windows' from version 10.0 to 12.0, 'ios' from version 7.0 to 11.0.
        {|#2:UnsupportedWindows8_9_12_Ios6_7_14.UnsupportedWindows6_10_Ios_1_5_11.UnsupportedWindowsIos2_9_10()|}; // 'UnsupportedWindows8_9_12_Ios6_7_14.UnsupportedWindows6_10_Ios_1_5_11.UnsupportedWindowsIos2_9_10()' is supported on: 'windows' from version 10.0 to 12.0, 'ios' from version 9.0 to 10.0.
    }
}

[UnsupportedOSPlatform(""windows7.0"")]
[SupportedOSPlatform(""windows9.0"")]
[UnsupportedOSPlatform(""windows12.0"")]
[UnsupportedOSPlatform(""ios6.0"")]
[SupportedOSPlatform(""ios7.0"")]
[UnsupportedOSPlatform(""ios14.0"")]
class UnsupportedWindows8_9_12_Ios6_7_14
{
    public static void ApiWithNoAttrbiute() { }
    
    [UnsupportedOSPlatform(""windows6.0"")] // will override
    [SupportedOSPlatform(""windows10.0"")]
    [UnsupportedOSPlatform(""ios1.0"")]
    [SupportedOSPlatform(""ios5.0"")]
    [UnsupportedOSPlatform(""ios11.0"")]
    internal static class UnsupportedWindows6_10_Ios_1_5_11
    {
        public static void ApiWithNoAttrbiute() { }

        [UnsupportedOSPlatform(""windows"")]
        [UnsupportedOSPlatform(""ios2.0"")]
        [SupportedOSPlatform(""ios9.0"")]
        [UnsupportedOSPlatform(""ios10.0"")]
        public static void UnsupportedWindowsIos2_9_10() { }
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(0).WithArguments("UnsupportedWindows8_9_12_Ios6_7_14.ApiWithNoAttrbiute()",
                    Join(GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "ios", "7.0", "14.0"),
                         GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "maccatalyst", "7.0", "14.0"),
                         GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "9.0", "12.0"))),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(1).WithArguments("UnsupportedWindows8_9_12_Ios6_7_14.UnsupportedWindows6_10_Ios_1_5_11.ApiWithNoAttrbiute()",
                    Join(GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "ios", "7.0", "11.0"),
                         GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "maccatalyst", "7.0", "11.0"),
                         GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "12.0"))),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(2).WithArguments("UnsupportedWindows8_9_12_Ios6_7_14.UnsupportedWindows6_10_Ios_1_5_11.UnsupportedWindowsIos2_9_10()",
                    Join(GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "ios", "9.0", "10.0"),
                         GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "maccatalyst", "9.0", "10.0"),
                         GetFormattedString(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityFromVersionToVersion, "windows", "10.0", "12.0"))));
        }

        [Fact]
        public async Task EmptyCallsiteReferencesLocalFunctionNotWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;
[SupportedOSPlatform(""Browser"")]
public class Test
{
    [UnsupportedOSPlatform(""browser"")]
    public static string CurrentProgram
    {
        get
        {
            return EnsureInitialized();
            string EnsureInitialized() { return string.Empty; }
        }
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task NarrowedCallsiteReferencesLocalFunctionNotWarnsAsync()
        {
            var source = @"
using System.Runtime.Versioning;
[SupportedOSPlatform(""Browser"")]
public class Test
{
    [UnsupportedOSPlatform(""browser2.0"")]
    public static string CurrentProgram
    {
        get
        {
            return EnsureInitialized();
            string EnsureInitialized() { return string.Empty; }
        }
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task EmptyCallsiteReferencesApiWithinSameAssemblyAttributeNotWarnAsync()
        {
            var source = @"
using System.Runtime.Versioning;

[assembly:SupportedOSPlatform(""Browser"")] 
public class Test
{
    private string program;

    [UnsupportedOSPlatform(""browser"")] // unsupports the only supported platform of parent producing empty call site, not warning
    public string CurrentProgram
    {
        get
        {
            return program; // should not warn
        }
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task EmptyCallsiteReferencesApiWithImmediateAttributeNotWarnAsync()
        {
            var source = @"
using System.Runtime.Versioning;

[assembly:SupportedOSPlatform(""Browser"")]
public class Test
{
    [SupportedOSPlatform(""Browser"")]
    private static string s_program;

    [UnsupportedOSPlatform(""browser"")]
    public static string CurrentProgram
    {
        get
        {
            return s_program; // should not warn
        }
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task EmptyCallsiteReferencesPlatformSpecificLibrariesApisNotWarnAsync()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

[assembly:SupportedOSPlatform(""Browser"")] 
public class Test
{
    [UnsupportedOSPlatform(""browser"")] // Overrides only support parent had, no valid call sites, not warm for anything
    public void M1()
    {
        Console.Beep(); // Unsupported on browser API, should not warn
        Console.Beep(10, 20); // Windows only API, should not warn
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task Empty_NonCallsiteReferencesVersionedApiWithImmediateAttributeAsync()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

[SupportedOSPlatform(""windows7.0"")]
public class Test
{
    [UnsupportedOSPlatform(""windows9.0"")]
    void StillSupportedOnWindows7and8()
    {   // This call site is reachable on: 'windows' from version 7.0 to 9.0. 'Test.SupportedOnWindows10()' is only supported on: 'windows' 10.0 and later.
        [|SupportedOnWindows10()|]; // Warning; calling from Windows 7-8 is still supported but it will fail
    }
    [UnsupportedOSPlatform(""windows6.0"")]
    void NotSupportedAnywhere()
    {
        SupportedOnWindows10(); // No warning; no valid call sites
    }
    [SupportedOSPlatform(""windows10.0"")]
    void SupportedOnWindows10() { }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task ChildCanNarrowUpperLevelSupportedPlatformsAsync()
        {
            var source = @"
using System.Runtime.Versioning;

[assembly:SupportedOSPlatform(""windows"")]
[assembly:SupportedOSPlatform(""ios"")]
public class Test
{
    [SupportedOSPlatform(""windows"")]
    private void WindowsOnly() { }

    [SupportedOSPlatform(""ios"")]
    private void IosOnly () { }

    private void NoAttribute () { }

    public void M1 ()
    {
        [|WindowsOnly()|];
        [|IosOnly()|];
        NoAttribute();
    }
    [SupportedOSPlatform(""Windows"")]
    public void M2 ()
    {
       WindowsOnly();
       [|IosOnly()|];
        NoAttribute();
    }
    [SupportedOSPlatform(""iOS"")]
    public void M3 ()
    {
        [|WindowsOnly()|];
        IosOnly();
        NoAttribute();
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task EmptyCallsiteReferencesSupportedUnsupportedApisNotWarnsAsync()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

[SupportedOSPlatform(""browser"")]
[SupportedOSPlatform(""windows"")]
[SupportedOSPlatform(""macos"")]
class Test
{
    [UnsupportedOSPlatform(""browser"")]
    private static void ApiUnsupportedOnBrowser () { }

    [SupportedOSPlatform(""macos"")]
    class TestMacOs // Inside only mac accessable
    {
        [SupportedOSPlatform(""windows"")]
        void WindowsOnly() // The method attribute causes not valid call sites; no warnings no matter what
        {
            Test.ApiUnsupportedOnBrowser();
            Console.Beep(10, 20); // Windows only API
        }
    }

    void MethodHasNoAttribute()
    {
        [|ApiUnsupportedOnBrowser()|]; // This call site is reachable on: 'macos', 'browser', 'windows'. 'Test.ApiUnsupportedOnBrowser()' is unsupported on: 'browser'.
        [|Console.Beep(10, 20)|];      // This call site is reachable on: 'macos', 'browser', 'windows'. 'Console.Beep(int, int)' is only supported on: 'windows'.
    }

    [SupportedOSPlatform(""WINDOWS"")]
    void WindowsOnly() // Nothing should warn
    {
        ApiUnsupportedOnBrowser();
        Console.Beep(10, 20);
    }

    [SupportedOSPlatform(""Linux"")] // Not valid because Linux is not in parent list of platforms
    void LinuxOnly() // => not valid call site; no warnings no matter what
    {
        ApiUnsupportedOnBrowser();
        Console.Beep(10, 20);
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        [WorkItem(4920, "https://github.com/dotnet/roslyn-analyzers/issues/4920")]
        public async Task TestTimelyTerminationAsync()
        {
            var source = @"
using System.Runtime.InteropServices;
using Microsoft.Win32;

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Registry.GetValue("""", """", null) != null)
{
    foreach (var x in new string[0])
    {
        if ("""".ToString() == """")
        {
            try
            {
            }
            catch
            {
            }
        }
    }
}";

            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50.AddPackages(
                    ImmutableArray.Create(new PackageIdentity("Microsoft.Win32.Registry", "5.0.0"))),
                LanguageVersion = CSharpLanguageVersion.CSharp9,
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources = { source },
                    AnalyzerConfigFiles = { ("/.globalconfig", $@"is_global = true

{s_msBuildPlatforms}") },
                }
            }.RunAsync();
        }

        [Fact, WorkItem(51652, "https://github.com/dotnet/roslyn/issues/51652")]
        public async Task TestGuardedCheckInsideLoopWithTryCatchAsync()
        {
            var source = @"
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;

class TestType
{
    static async Task Main(string[] args) {
	while (true)
		try {
			await Task.Delay(1000);
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				Test();
		} catch {
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				Test();
			throw;
		}
    }

    [SupportedOSPlatform(""windows"")]
    static void Test() { }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact, WorkItem(6015, "https://github.com/dotnet/roslyn-analyzers/issues/6015")]
        public async Task TestGuardedCheckInsideLoopWithIfAsync()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

class C
{
    void M(IEnumerable<D> list)
    {
        foreach (var d in list)
        {
            if ([|d.Flag|]) // This call site is reachable on all platforms. 'C.D.Flag' is only supported on: 'Windows'.
            {
                if (OperatingSystem.IsWindows() && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763, 0))
                {
                }
            }
        }
    }

    [SupportedOSPlatform(""Windows"")]
    private class D
    {
        public bool Flag { get; }
    }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task IosSupportedOnMacCatalystAsync()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

class TestType
{
    [SupportedOSPlatform(""ios"")]
    private void SupportsIOS() { }

    [SupportedOSPlatform(""maccatalyst"")]
    internal void SupportsMacCatalyst() { }

    [SupportedOSPlatform(""ios"")]
    [SupportedOSPlatform(""Linux"")]
    [SupportedOSPlatform(""maccatalyst"")]
    void SupportsIOSLinuxMacCatalyst() { }

    [UnsupportedOSPlatform(""maccatalyst"")]
    static void UnsupportsMacCatalyst() { }

    [UnsupportedOSPlatform(""ios"")]
    static void UnsupportsIos() { }

    void CrossPlatformMethod()
    {
        [|SupportsIOSLinuxMacCatalyst()|]; // This call site is reachable on all platforms. 'TestType.SupportsIOSLinuxMacCatalyst()' is only supported on: 'ios', 'Linux', 'maccatalyst'.
        [|SupportsMacCatalyst()|];         // This call site is reachable on all platforms. 'TestType.SupportsMacCatalyst()' is only supported on: 'maccatalyst'.
        [|SupportsIOS()|];                 // This call site is reachable on all platforms. 'TestType.SupportsIOS()' is only supported on: 'ios', 'maccatalyst'.
        UnsupportsIos();                   // no warning because not in the MSBuild default list
        UnsupportsMacCatalyst();
    }

    [SupportedOSPlatform(""iOS"")]
    void MethodReachableOnIOS()
    {
        SupportsIOSLinuxMacCatalyst();
        [|SupportsMacCatalyst()|];       // This call site is reachable on: 'iOS', 'maccatalyst'. 'TestType.SupportsMacCatalyst()' is only supported on: 'maccatalyst'.
        SupportsIOS();
        [|UnsupportsIos()|];             // This call site is reachable on: 'iOS', 'maccatalyst'. 'TestType.UnsupportsIos()' is unsupported on: 'ios', 'maccatalyst'.
        [|UnsupportsMacCatalyst()|];     // This call site is reachable on: 'iOS', 'maccatalyst'. 'TestType.UnsupportsMacCatalyst()' is unsupported on: 'maccatalyst'.
    }

    [SupportedOSPlatform(""MacCatalyst"")]
    void MethodReachableOnMacCatalyst()
    {
        SupportsIOSLinuxMacCatalyst();
        SupportsMacCatalyst();
        SupportsIOS();
        [|UnsupportsIos()|];             // This call site is reachable on: 'MacCatalyst'. 'TestType.UnsupportsIos()' is unsupported on: 'maccatalyst'.
        [|UnsupportsMacCatalyst()|];     // This call site is reachable on: 'MacCatalyst'. 'TestType.UnsupportsMacCatalyst()' is unsupported on: 'maccatalyst'.
    }

    [SupportedOSPlatform(""ios"")]
    [UnsupportedOSPlatform(""MacCatalyst"")]
    void MethodReachableOnIOSButNotMacCatalyst()
    {
        SupportsIOSLinuxMacCatalyst();
        [|SupportsMacCatalyst()|];     // This call site is reachable on: 'ios'. 'TestType.SupportsMacCatalyst()' is only supported on: 'maccatalyst'.    
        SupportsIOS();
        [|UnsupportsIos()|];            // This call site is reachable on: 'ios'. 'TestType.UnsupportsIos()' is unsupported on: 'ios'.
        UnsupportsMacCatalyst();    
    }
}";

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task ExcludeMacCatalystSupportUnsupportFromIosAsync()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

[SupportedOSPlatform(""ios"")]
class SupportsIos
{
    [UnsupportedOSPlatform(""MacCatalyst"")]
    public static void SupportsIOSNotMacCatalyst() { }

    [UnsupportedOSPlatform(""ios"")] // only removes iOS support
    [SupportedOSPlatform(""maccatalyst"")]
    public static void WorksOnMacCatalystNotIOS() { }
}

class AllPlatforms
{
    [UnsupportedOSPlatform(""ios"")]
    [SupportedOSPlatform(""maccatalyst"")] // only removes inferred unsupport
    public void WorksEverywhereExceptIOS_ButDoesWorkOnMacCatalyst() { }

    [SupportedOSPlatform(""ios"")]
    [UnsupportedOSPlatform(""MacCatalyst"")]
    public static void SupportsIOSNotMacCatalyst() { }

    void CrossPlatformMethod()
    {
        [|SupportsIOSNotMacCatalyst()|];               // This call site is reachable on all platforms. 'AllPlatforms.SupportsIOSNotMacCatalyst()' is only supported on: 'ios'.
        WorksEverywhereExceptIOS_ButDoesWorkOnMacCatalyst(); // no diagnostic because MSBuild list is not added
        [|SupportsIos.SupportsIOSNotMacCatalyst()|];   // This call site is reachable on all platforms. 'TestType.SupportsIOSNotMacCatalyst()' is only supported on: 'ios'.
        [|SupportsIos.WorksOnMacCatalystNotIOS()|];    // This call site is reachable on all platforms. 'TestType.WorksOnMacCatalystNotIOS()' is supported on: 'maccatalyst'.
    }

    [SupportedOSPlatform(""iOS"")]
    void MethodReachableOnIOS()
    {
        [|SupportsIOSNotMacCatalyst()|];             // This call site is reachable on: 'iOS', 'maccatalyst'. 'AllPlatforms.SupportsIOSNotMacCatalyst()' is only supported on: 'ios'.
        [|WorksEverywhereExceptIOS_ButDoesWorkOnMacCatalyst()|]; // This call site is reachable on: 'iOS', 'maccatalyst'. 'AllPlatforms.WorksEverywhereExceptIOS_ButDoesWorkOnMacCatalyst()' is unsupported on: 'ios'.
        [|SupportsIos.SupportsIOSNotMacCatalyst()|]; // This call site is reachable on: 'iOS', 'maccatalyst'. 'SupportsIos.SupportsIOSNotMacCatalyst()' is unsupported on: 'maccatalyst'.
        [|SupportsIos.WorksOnMacCatalystNotIOS()|];  // This call site is reachable on: 'iOS', 'maccatalyst'. 'SupportsIos.WorksOnMacCatalystNotIOS()' is unsupported on: 'ios'.
    }

    [SupportedOSPlatform(""MacCatalyst"")]
    void MethodReachableOnMacCatalyst()
    {
        [|SupportsIOSNotMacCatalyst()|];             // This call site is reachable on: 'MacCatalyst'. 'AllPlatforms.SupportsIOSNotMacCatalyst()' is only supported on: 'ios'.
        WorksEverywhereExceptIOS_ButDoesWorkOnMacCatalyst();
        [|SupportsIos.SupportsIOSNotMacCatalyst()|]; // This call site is reachable on: 'MacCatalyst'. 'SupportsIos.SupportsIOSNotMacCatalyst()' is unsupported on: 'maccatalyst'.
        SupportsIos.WorksOnMacCatalystNotIOS();
    }

    [SupportedOSPlatform(""ios"")]
    [UnsupportedOSPlatform(""MacCatalyst"")]
    void MethodReachableOnIOSButNotMacCatalyst()
    {
        SupportsIOSNotMacCatalyst(); 
        [|WorksEverywhereExceptIOS_ButDoesWorkOnMacCatalyst()|]; // This call site is reachable on: 'ios'. 'AllPlatforms.WorksEverywhereExceptIOS_ButDoesWorkOnMacCatalyst()' is unsupported on: 'ios'.
        SupportsIos.SupportsIOSNotMacCatalyst();
        [|SupportsIos.WorksOnMacCatalystNotIOS()|]; // This call site is reachable on: 'ios'. 'SupportsIos.WorksOnMacCatalystNotIOS()' is supported on: 'maccatalyst'.
    }

    [UnsupportedOSPlatform(""ios"")]
    [SupportedOSPlatform(""MacCatalyst"")] // only removes inferred unsupport
    void MethodUnreachableOnIOSButReachableMacCatalyst()
    {
        [|SupportsIOSNotMacCatalyst()|];             //  This call site is unreachable on: 'ios'. 'AllPlatforms.SupportsIOSNotMacCatalyst()' is only supported on: 'ios'.
        WorksEverywhereExceptIOS_ButDoesWorkOnMacCatalyst();
        [|SupportsIos.SupportsIOSNotMacCatalyst()|]; // This call site is unreachable on: 'ios'. 'SupportsIos.SupportsIOSNotMacCatalyst()' is only supported on: 'ios'.
        [|SupportsIos.WorksOnMacCatalystNotIOS()|];  // This call site is reachable on all platforms. 'SupportsIos.WorksOnMacCatalystNotIOS()' is supported on: 'maccatalyst'.  
    }
}";

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task IosSupportedOnMacCatalystVersionedAsync()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

class TestType
{
    [SupportedOSPlatform(""iOS10.0"")]
    static void MethodVersionNotSuppress()
    {
        [|SupportsMacCatalyst()|]; // This call site is reachable on: 'iOS' 10.0 and later, 'maccatalyst' 10.0 and later. 'TestType.SupportsMacCatalyst()' is only supported on: 'maccatalyst' 12.0 and later.
        [|SupportsIOS()|]; // This call site is reachable on: 'iOS' 10.0 and later, 'maccatalyst' 10.0 and later. 'TestType.SupportsIOS()' is only supported on: 'ios' 12.0 and later, 'maccatalyst' 12.0 and later.
    }

    [SupportedOSPlatform(""iOS14.0"")]
    static void IOSMethod()
    {
        [|SupportsMacCatalyst()|]; // This call site is reachable on: 'iOS' 14.0 and later, 'maccatalyst' 14.0 and later. 'TestType.SupportsMacCatalyst()' is only supported on: 'maccatalyst' 12.0 and later.
        SupportsIOS();
    }

    [SupportedOSPlatform(""maccatalyst14.0"")]
    static void MacCatalystMethod()
    {
        SupportsMacCatalyst();
        SupportsIOS();
    }

    [SupportedOSPlatform(""maccatalyst12.0"")]
    static void SupportsMacCatalyst() { }

    [SupportedOSPlatform(""ios12.0"")]
    static void SupportsIOS() { }
}";

            await VerifyAnalyzerCSAsync(source);
        }

        [Fact]
        public async Task IosUnsupportedOnMacCatalystAsync()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

class TestType
{
    static void M1()
    {
        [|UnsupportsMacCatalyst()|]; // This call site is reachable on all platforms. 'TestType.UnsupportsMacCatalyst()' is unsupported on: 'maccatalyst'.
        [|UnsupportsIos()|];         // This call site is reachable on all platforms. 'TestType.UnsupportsIos()' is unsupported on: 'iOS', 'maccatalyst'.
    }

    [UnsupportedOSPlatform(""iOS"")]
    static void NonIOSMethod()
    {
        UnsupportsMacCatalyst();
        UnsupportsIos();
    }

    [UnsupportedOSPlatform(""maccatalyst"")]
    static void NonMacCatalystMethod()
    {
        UnsupportsMacCatalyst();
        [|UnsupportsIos()|];     // This call site is reachable on all platforms. 'TestType.UnsupportsIos()' is unsupported on: 'iOS'.
    }

    [UnsupportedOSPlatform(""maccatalyst"")]
    static void UnsupportsMacCatalyst() { }
    
    [UnsupportedOSPlatform(""iOS"")]
    static void UnsupportsIos() { }
}";

            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task IosUnsupportedOnMacCatalystVersionedAsync()
        {
            var source = @"
using System;
using System.Runtime.Versioning;

class TestType
{
    [UnsupportedOSPlatform(""iOS10.0"")]
    static void IosVersionSuppressed()
    {
        UnsupportsMacCatalyst();
        UnsupportsIos();
    }

    [UnsupportedOSPlatform(""maccatalyst10.0"")]
    static void NonMacCatalystMethod()
    {
        UnsupportsMacCatalyst();
        [|UnsupportsIos()|];    // This call site is reachable on all platforms. 'TestType.UnsupportsIos()' is unsupported on: 'iOS' 12.0 and later.
    }

    [UnsupportedOSPlatform(""iOS14.0"")]
    static void IosVersionNotSuppress()
    {
        [|UnsupportsMacCatalyst()|]; // This call site is reachable on: 'maccatalyst' 14.0 and before. 'TestType.UnsupportsMacCatalyst()' is unsupported on: 'maccatalyst' 12.0 and later.
        [|UnsupportsIos()|];         // This call site is reachable on: 'iOS' 14.0 and before, 'maccatalyst' 14.0 and before. 'TestType.UnsupportsIos()' is unsupported on: 'iOS' 12.0 and later, 'maccatalyst' 12.0 and later.
    }

    [UnsupportedOSPlatform(""maccatalyst12.0"")]
    static void UnsupportsMacCatalyst() { }

    [UnsupportedOSPlatform(""iOS12.0"")]
    static void UnsupportsIos() { }
}";
            await VerifyAnalyzerCSAsync(source, s_msBuildPlatforms);
        }

        private string GetFormattedString(string resource, params string[] args) =>
            string.Format(CultureInfo.InvariantCulture, resource, args);

        private string Join(params string[] platforms) =>
            string.Join(MicrosoftNetCoreAnalyzersResources.CommaSeparator, platforms);

        private static VerifyCS.Test PopulateTestCs(string sourceCode, params DiagnosticResult[] expected)
        {
            var test = new VerifyCS.Test
            {
                TestCode = sourceCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
                TestState = { }
            };
            test.ExpectedDiagnostics.AddRange(expected);
            return test;
        }

        private static async Task VerifyAnalyzerCSAsync(string sourceCode, params DiagnosticResult[] expectedDiagnostics)
            => await VerifyAnalyzerCSAsync(sourceCode, "build_property.TargetFramework = net5\nbuild_property.TargetFrameworkIdentifier = .NETCoreApp\nbuild_property.TargetFrameworkVersion = v5.0", expectedDiagnostics);

        private static async Task VerifyAnalyzerCSAsync(string sourceCode, string editorconfigText, params DiagnosticResult[] expectedDiagnostics)
        {
            var test = PopulateTestCs(sourceCode, expectedDiagnostics);
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", $@"root = true

[*]
{editorconfigText}
"));
            await test.RunAsync();
        }

        private static async Task VerifyAnalyzerCSAsync(string sourceCode, string editorconfigText)
        {
            var test = PopulateTestCs(sourceCode);
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", $@"root = true

[*]
{editorconfigText}
"));
            await test.RunAsync();
        }

        private static async Task VerifyAnalyzerVBAsync(string sourceCode, params DiagnosticResult[] expectedDiagnostics)
            => await VerifyAnalyzerVBAsync(sourceCode, "build_property.TargetFramework = net5\nbuild_property.TargetFrameworkIdentifier = .NETCoreApp\nbuild_property.TargetFrameworkVersion = v5.0", expectedDiagnostics);

        private static async Task VerifyAnalyzerVBAsync(string sourceCode, string editorconfigText, params DiagnosticResult[] expectedDiagnostics)
        {
            var test = PopulateTestVb(sourceCode, expectedDiagnostics);
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", $@"root = true

[*]
{editorconfigText}
"));
            await test.RunAsync();
        }

        private static VerifyVB.Test PopulateTestVb(string sourceCode, params DiagnosticResult[] expected)
        {
            var test = new VerifyVB.Test
            {
                TestCode = sourceCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
                TestState = { },
            };
            test.ExpectedDiagnostics.AddRange(expected);
            return test;
        }

        private readonly string AllowDenyListTestClasses = @"
public class DenyList
{
    [UnsupportedOSPlatform(""windows10.0"")]
    public static void UnsupportedWindows10() { }

    [UnsupportedOSPlatform(""windows"")]
    public static void UnsupportedWindows() { }
    
    [UnsupportedOSPlatform(""windows"")]
    [SupportedOSPlatform(""windows8.0"")]
    [UnsupportedOSPlatform(""windows10.0"")]
    public static void UnsupportedSupportedWindows8To10() { }
}
    
public class AllowList
{
    [SupportedOSPlatform(""windows10.0"")]
    public static void Windows10Only() { }

    [SupportedOSPlatform(""windows"")]
    public static void WindowsOnly() { }

    [SupportedOSPlatform(""windows"")]
    [UnsupportedOSPlatform(""windows10.0"")]
    public static void WindowsOnlyUnsupportedFrom10() { }
    
    [SupportedOSPlatform(""windows10.0"")]
    [UnsupportedOSPlatform(""windows11.0"")]
    public static void Windows10OnlyUnsupportedFrom11() { }
}";
    }
}
