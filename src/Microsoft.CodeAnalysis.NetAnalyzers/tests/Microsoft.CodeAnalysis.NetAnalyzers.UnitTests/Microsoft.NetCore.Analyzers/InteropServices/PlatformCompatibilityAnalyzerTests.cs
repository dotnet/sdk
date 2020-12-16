// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
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
        private const string s_msBuildPlatforms = "build_property._SupportedPlatformList=windows,browser, ios;\nbuild_property.TargetFramework=net5.0";

        [Fact(Skip = "TODO need to be fixed: Test for for wrong arguments, not sure how to report the Compiler error diagnostic")]
        public async Task TestOsPlatformAttributesWithNonStringArgument()
        {
            var csSource = @"
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

public class Test
{
    [[|SupportedOSPlatform(""Linux"", ""Windows"")|]]
    public void MethodWithTwoArguments()
    {
    }
    [UnsupportedOSPlatform([|new string[]{""Linux"", ""Windows""}|])]
    public void MethodWithArrayArgument()
    {
    }
}
" + MockAttributesCsSource;

            await VerifyAnalyzerAsyncCs(csSource);
        }

        public static IEnumerable<object[]> Create_DifferentTfms()
        {
            yield return new object[] { "build_property.TargetFramework = net472", false };
            yield return new object[] { "build_property.TargetFramework = netcoreapp1.0", false };
            yield return new object[] { "build_property.TargetFramework = dotnet", false };
            yield return new object[] { "build_property.TargetFramework = uap10.0", false };
            yield return new object[] { "build_property.TargetFramework = netstandard2.1", false };
            yield return new object[] { "build_property.TargetFramework = net5", true };
            yield return new object[] { "build_property.TargetFramework = net5.0", true };
            yield return new object[] { "build_property.TargetFramework = net5.0-windows", true };
            yield return new object[] { "build_property.TargetFramework = net5.0-ios14.0", true };
            yield return new object[] { "build_property.TargetFramework = Net99", true };
            yield return new object[] { "build_property.TargetFramework = netcoreapp5", false };
        }

        [Theory]
        [MemberData(nameof(Create_DifferentTfms))]
        public async Task Net5OrHigherTfmWarns_LowerThanNet5NotWarn(string tfm, bool warn)
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source, tfm);
        }

        public static IEnumerable<object[]> Create_DifferentTfmsWithOption()
        {
            yield return new object[] { "build_property.TargetFramework = net472\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=true", true };
            yield return new object[] { "build_property.TargetFramework = net472\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", false };
            yield return new object[] { "build_property.TargetFramework = netcoreapp1.0\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=true", true };
            yield return new object[] { "build_property.TargetFramework = netcoreapp1.0\ndotnet_code_quality.CA1416.enable_platform_analyzer_on_pre_net5_target=false", false };
            yield return new object[] { "build_property.TargetFramework = dotnet\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=true", true };
            yield return new object[] { "build_property.TargetFramework = uap10.0\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", false };
            yield return new object[] { "build_property.TargetFramework = netstandard2.1\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=true", true };
            yield return new object[] { "build_property.TargetFramework = netstandard2.1\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", false };
            yield return new object[] { "build_property.TargetFramework = net5\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", true };
            yield return new object[] { "build_property.TargetFramework = net5.0\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", true };
            yield return new object[] { "build_property.TargetFramework = net5.0-windows\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", true };
            yield return new object[] { "build_property.TargetFramework = net5.0-ios14.0\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", true };
            yield return new object[] { "build_property.TargetFramework = net6.0\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", true };
            yield return new object[] { "build_property.TargetFramework = netcoreapp5\ndotnet_code_quality.enable_platform_analyzer_on_pre_net5_target=false", false };
        }

        [Theory]
        [MemberData(nameof(Create_DifferentTfmsWithOption))]
        public async Task Net5OrHigherTfmWarns_LowerThanNet5WarnsIfEnabled(string tfmAndOption, bool warn)
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
}" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source, tfmAndOption);
        }

        [Fact]
        public async Task OnlyThrowsNotSupportedWithOsDependentStringNotWarns()
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
}" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(csSource);
        }

        [Fact]
        public async Task ThrowNotSupportedWithOtherOsDependentApiUsageNotWarns()
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
        [|SR.Message|] = ""Warns not reachable on Browser"";
        throw new PlatformNotSupportedException(SR.Message);
    }

    void ThrowNotSupportedWithStringArgument()
    {
        [|SR.Message|] = ""Warns not reachable on Browser"";
        throw new NotSupportedException(SR.Message);
    }
    
    void ThrowWithNoArgument()
    {
        [|SR.Message|] = ""Warns not reachable on Browser"";
        throw new PlatformNotSupportedException();
    }
    
    void ThrowWithStringAndExceptionConstructor()
    {
        [|SR.Message|] = ""Warns not reachable on Browser"";
        throw new PlatformNotSupportedException(SR.Message, new Exception());
    }
    
    void ThrowWithAnotherExceptionUsingResourceString()
    {
        [|SR.Message|] = ""Warns not reachable on Browser"";
        throw new PlatformNotSupportedException(SR.Message, new Exception([|SR.Message|]));
    }
}" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(csSource);
        }

        [Fact]
        public async Task ThrowNotSupportedWithOtherStatementAndWithinConditionNotWarns()
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
        if (!OperatingSystemHelper.IsBrowser())
        {
            throw new PlatformNotSupportedException(SR.Message);
        }
        SR.M1();

        [|Windows.Message|] = ""Warns supported only on Windows"";
        if (!OperatingSystemHelper.IsWindows())
        {
            throw new NotSupportedException(Windows.Message);
        }
        Windows.Message = ""It is windows!"";
    }

    void ThrowWithOtherConstructorWarnsForInnnerException()
    {
        [|SR.M1()|];
        if (!OperatingSystemHelper.IsBrowser())
        {
            throw new PlatformNotSupportedException();
        }
        SR.M1();

        [|Windows.Message|] = ""Warns supported only on Windows"";
        if (!OperatingSystemHelper.IsWindows())
        {
            throw new NotSupportedException(Windows.Message, new Exception([|Windows.Message|]));
        }
    }
}" + MockAttributesCsSource + MockOperatingSystemApiSource;
            await VerifyAnalyzerAsyncCs(csSource);
        }

        [Fact]
        public async Task CreateNotSupportedWithOsDependentStringNotWarns()
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
        [|s_message|] = ""Warns not reachable on Browser"";
        return new NotSupportedException(s_message); 
    }

    [UnsupportedOSPlatform(""browser"")]
    public static Exception ThrowPnseWithStringWarnsForInnerException()
    { 
        return new PlatformNotSupportedException(s_message, new Exception([|s_message|]));
    }

    [UnsupportedOSPlatform(""browser"")]
    public static Exception ThrowPnseDefaultConstructor() { return new PlatformNotSupportedException(); }
}" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(csSource);
        }

        [Fact]
        public async Task ThrowNotSupportedWarnsForNonStringArgument()
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
        if (!OperatingSystemHelper.IsWindows())
        {
            throw new NotSupportedException(WindowsOnlyException.Message, [|new WindowsOnlyException()|]);
        }
    }
}" + MockAttributesCsSource + MockOperatingSystemApiSource;
            await VerifyAnalyzerAsyncCs(csSource);
        }

        [Fact]
        public async Task OsDependentMethodsCalledWarns()
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
    public void Unsupported()
    {
    }
    [SupportedOSPlatform(""Windows10.1.1.1"")]
    public void WindowsOnly()
    {
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(csSource);

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
End Class
" + MockAttributesVbSource;
            await VerifyAnalyzerAsyncVb(vbSource);
        }

        [Fact]
        public async Task WrongPlatformStringsShouldHandledGracefully()
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
    public void NotForWindows()
    {
    }
    [SupportedOSPlatform(""Windows10"")]
    public void Windows10()
    {
    }
    [SupportedOSPlatform(""Windows1.2.3.4.5"")]
    public void Windows1_2_3_4_5()
    {
    }
    [SupportedOSPlatform(""Ios-4.1"")]
    public void UnsupportedOSPlatformIosDash4_1()
    {
    }
    [SupportedOSPlatform(""Ios*4.1"")]
    public void UnsupportedOSPlatformIosStar4_1()
    {
    }
    [SupportedOSPlatform(null)]
    public void UnsupportedOSPlatformWithNullString()
    {
    }
    [SupportedOSPlatform(""Linu4.1"")]
    public void SupportedLinu4_1()
    {
    }
    [UnsupportedOSPlatform("""")]
    public void UnsupportedWithEmptyString()
    {
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task OsDependentPropertyCalledWarns()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact, WorkItem(4071, "https://github.com/dotnet/roslyn-analyzers/issues/4071")]
        public async Task OsDependentPropertyGetterSetterCalledWarns()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);

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
End Class
" + MockAttributesVbSource;
            await VerifyAnalyzerAsyncVb(vbSource);
        }

        [Theory]
        [MemberData(nameof(Create_AttributeProperty_WithCondtions))]
        public async Task OsDependentPropertyConditionalCheckWarns(string attribute, string property, string condition, string setter, string getter)
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        public static IEnumerable<object[]> Create_AttributeProperty_WithCondtions()
        {
            yield return new object[] { "SupportedOSPlatform", "string StringProperty", " == [|StringProperty|]", @"StringProperty|] = ""Hello""", "StringProperty" };
            yield return new object[] { "UnsupportedOSPlatform", "int UnsupportedProperty", " <= [|UnsupportedProperty|]", "UnsupportedProperty|] = 3", "UnsupportedProperty" };
        }

        [Fact]
        public async Task OsDependentEnumValueCalledWarns()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task OsDependentEnumConditionalCheckNotWarns()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);

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
    < SupportedOSPlatform(""Linux4.8"") >
    Linux48
    NoPlatform
End Enum
" + MockAttributesVbSource;
            await VerifyAnalyzerAsyncVb(vbSource);
        }

        [Fact]
        public async Task OsDependentFieldCalledWarns()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task OsDependentMethodCalledFromInstanceWarns()
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
    public void M2()
    {
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task OsDependentMethodCalledFromOtherNsInstanceWarns()
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
        public void M2()
        {
        }
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);

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
End Namespace
" + MockAttributesVbSource;
            await VerifyAnalyzerAsyncVb(vbSource);
        }

        [Fact]
        public async Task OsDependentConstructorOfClassUsedCalledWarns()
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
    public C()
    {
    }
    public void M2()
    {
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task ConstructorAndMethodOfOsDependentClassCalledWarns()
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
    public void M2()
    {
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);

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
" + MockAttributesVbSource;
            await VerifyAnalyzerAsyncVb(vbSource);
        }

        [Fact]
        public async Task LocalFunctionCallsOsDependentMemberWarns()
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
    public void M2()
    {
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LocalGuardedFunctionCallsOsDependentMemberNotWarns()
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
    public void M2()
    {
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LocalFunctionEscapedCallsOsDependentMemberWarns()
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
    public void M2()
    {
    }

    public void M3(Action a) { a(); }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LocalFunctionUnusedCallsOsDependentMemberWarns()
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
    public void M2()
    {
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LambdaCallsOsDependentMemberWarns()
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
    public void M2()
    {
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task AttributedLambdaCallsOsDependentMemberNotWarn()
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
    public void M2()
    {
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LambdaEscapedCallsOsDependentMemberWarns()
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
    public void M2()
    {
    }

    public void M3(Action a) { a(); }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task LambdaUnusedCallsOsDependentMemberWarns()
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
    public void M2()
    {
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task OsDependentEventAccessedWarns()
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

    public void M3()
    {
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);

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
End Class"
+ MockAttributesVbSource;
            await VerifyAnalyzerAsyncVb(vbSource);
        }

        [Fact]
        public async Task EventOfOsDependentTypeAccessedWarns()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task OsDependentEventAddRemoveAccessedWarns()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task OsDependentMethodAssignedToDelegateWarns()
        {
            var source = @"
using System.Runtime.Versioning;

public class Test
{
    public delegate void Del(); // The attribute not supported on delegates, so no tests for that

    [SupportedOSPlatform(""Windows10.1.2.3"")]
    public void DelegateMethod()
    {
    }
    public void M1()
    {
        Del handler = [|DelegateMethod|];
        handler();
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact, WorkItem(4168, "https://github.com/dotnet/roslyn-analyzers/issues/4168")]
        public async Task UnsupportedShouldNotSuppressSupportedWithSameVersion()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact, WorkItem(4168, "https://github.com/dotnet/roslyn-analyzers/issues/4168")]
        public async Task CallSitesUnsupportedShouldNotSuppressSupportedWithSameVersion()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact, WorkItem(4168, "https://github.com/dotnet/roslyn-analyzers/issues/4168")]
        public async Task CallSiteUnsupportedShouldNotSuppressSupportedWithSameVersionAndSameLevel()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task CallerSupportsSubsetOfTarget()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task CallerUnsupportsNonSubsetOfTargetSupport()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task CallerUnsupportsSubsetOfTargetUsupportedFirstThenSupportsNotWarn()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task CallerUnsupportsNonSubsetOfTargetUnsupportedFirstSupportsWarns()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
        }

        [Fact]
        public async Task CallerSupportsSupersetOfTarget_AnotherScenario()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(0).WithArguments("Target.SupportedOnWindows()", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsUnreachable).WithLocation(1).WithArguments("Target.SupportedOnBrowser()", "'browser'", "'browser'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(2).WithArguments("Target.SupportedOnWindowsAndUnsupportedOnBrowser()", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(3).WithArguments("Target.UnsupportedOnWindows()", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(4).WithArguments("Target.UnsupportedOnWindows11()", "'windows' 11.0 and later", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(5).WithArguments("Target.UnsupportedOnWindowsAndBrowser()", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(6).WithArguments("Target.UnsupportedOnWindowsUntilWindows11()", "'windows' 11.0 and later", ""));
        }

        [Fact]
        public async Task CallerSupportsSupersetOfTarget()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task UnsupportedSamePlatformMustSuppressSupported()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task PlatformOverrides()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task ChildUnsupportedMustParentSupportedPlatformMustNotIgnored()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task SupportedMustSuppressUnsupportedAssemblyAttribute()
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
    public class BrowserApis
    {
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task UsingUnsupportedApiWithinAllowListShouldWarn()
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
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task UsingVersionedApiFromAllowListAssemblyNotIgnored()
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
    public static void WindowsSpecificApi()
    {
    }
}

[SupportedOSPlatform(""windows10.0"")] // This attribute will not be ignored
static class Some
{
    public static void Windows10SpecificApi()
    {
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact]
        public async Task ReintroducingHigherApiSupport_Warn()
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
    public static void WindowsSpecificApi11()
    {
    }

    [SupportedOSPlatform(""windows1.0"")]
    public static void WindowsSpecificApi1()
    {
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source);
        }

        [Fact(Skip = "TODO: enable when tests could consume new attribute applied to runtime assemblies")]
        public async Task MethodOfOsDependentAssemblyCalledWithoutSuppressionWarns()
        {
            var source = @"
            using System.Runtime.Versioning;
            using ns;
            public class Test
            {
                public void M1()
                {
                    OsDependentClass odc = new OsDependentClass();
                    odc.M2();
                }
            }
            [assembly:SupportedOSPlatform(""Windows10.1.2.3"")]
            namespace ns
            {
                public class OsDependentClass
                {
                    public void M2()
                    {
                    }
                }
            }
" + MockAttributesCsSource;
            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithSpan(10, 21, 10, 29).WithArguments("M2", "Windows", "10.1.2.3"));
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
            yield return new object[] { "Windows", "10.1.2.3", "Osx", "11.1.1.4", true };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "10.0.1.9", true };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "10.1.1.4", true };
            yield return new object[] { "Windows", "10.1.2.3", "Windows", "8.2.3.3", true };
        }

        [Theory]
        [MemberData(nameof(SupportedOsAttributeTestData))]
        public async Task MethodOfOsDependentClassSuppressedWithSupportedOsAttribute(string platform, string version, string suppressingPlatform, string suppressingVersion, bool warn)
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
    public void M2()
    {
    }
}
" + MockAttributesCsSource;

            if (warn)
            {
                await VerifyAnalyzerAsyncCs(source,
                    VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithSpan(9, 32, 9, 54).
                    WithArguments("OsDependentClass", $"'{platform}' {version} and later", $"'{suppressingPlatform}' {suppressingVersion} and later"),
                    VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithSpan(10, 9, 10, 17).
                    WithArguments("OsDependentClass.M2()", $"'{platform}' {version} and later", $"'{suppressingPlatform}' {suppressingVersion} and later"));
            }
            else
            {
                await VerifyAnalyzerAsyncCs(source);
            }
        }

        public static IEnumerable<object[]> UnsupportedAttributeTestData()
        {
            yield return new object[] { "windows", "10.1.2.3", "Windows", "10.1.2.3", false };
            yield return new object[] { "windows", "10.1.2.3", "Mac", "Os10.1.3.3", true };
            /*yield return new object[] { "windows", "10.1.2.3", "Windows", "10.1.3.1", true };
            yield return new object[] { "windows", "10.1.2.3", "Windows", "11.1", true };
            yield return new object[] { "windows", "10.1.2.3", "Windows", "10.2.2.0", true };*/
            yield return new object[] { "windows", "10.1.2.3", "Windows", "10.1.1.3", false };
            yield return new object[] { "windows", "10.1.2.3", "Windows", "10.1.1.3", false };
            yield return new object[] { "windows", "10.1.2.3", "Windows", "10.1.1.4", false };
            yield return new object[] { "windows", "10.1.2.3", "Osx", "10.1.1.4", true };
            yield return new object[] { "windows", "10.1.2.3", "Windows", "10.1.0.1", false };
            yield return new object[] { "windows", "10.1.2.3", "Windows", "8.2.3.4", false };
        }

        [Theory]
        [MemberData(nameof(UnsupportedAttributeTestData))]
        public async Task MethodOfOsDependentClassSuppressedWithUnsupportedAttribute(string platform,
            string version, string suppressingPlatform, string suppressingVersion, bool warn)
        {
            var source = @"
 using System.Runtime.Versioning;
 
[UnsupportedOSPlatform(""" + suppressingPlatform + suppressingVersion + @""")]
public class Test
{
    public void M1()
    {
        OsDependentClass odc = new OsDependentClass();
        odc.M2();
    }
}

[UnsupportedOSPlatform(""" + platform + version + @""")]
public class OsDependentClass
{
    public void M2()
    {
    }
}
" + MockAttributesCsSource;

            if (warn)
            {
                if (platform.Equals(suppressingVersion, System.StringComparison.OrdinalIgnoreCase))
                {
                    await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms, VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(9, 32).
                        WithArguments("OsDependentClass", $"'{platform}' {version} and later", $"'{suppressingPlatform}' {suppressingVersion}  and before"),
                        VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(10, 9).
                        WithArguments("OsDependentClass.M2()", $"'{platform}' {version} and later", $"'{suppressingPlatform}' {suppressingVersion}  and before"));
                }
                else
                {
                    await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms, VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(9, 32).
                        WithArguments("OsDependentClass", $"'{platform}' {version} and later", string.Empty),
                        VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(10, 9).
                        WithArguments("OsDependentClass.M2()", $"'{platform}' {version} and later", string.Empty));
                }
            }
            else
            {
                await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms);
            }
        }

        [Fact]
        public async Task UnsupportedNotWarnsForUnrelatedSupportedContext()
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
        C.StaticClass.LinuxMethod();
        C.StaticClass.LinuxVersionedMethod();
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
}
" + MockAttributesCsSource;

            await VerifyAnalyzerAsyncCs(source, VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).
                WithLocation(11, 9).WithArguments("C.StaticClass.LinuxMethod()", "'linux'", "'Linux'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).
                WithLocation(12, 9).WithArguments("C.StaticClass.LinuxVersionedMethod()", "'linux' 4.8 and later", "'Linux' all versions"));
        }

        [Fact]
        public async Task MultipleAttrbiutesOptionallySupportedListTest()
        {
            var source = @"
 using System.Runtime.Versioning;
 
public class Test
{
    C obj = new C();
    [SupportedOSPlatform(""Linux"")]
    public void DiffferentOsNotWarn()
    {
        obj.DoesNotWorkOnWindows();
    }

    [SupportedOSPlatform(""windows"")]
    [UnsupportedOSPlatform(""windows10.0.2000"")]
    public void SupporteWindows()
    {
        // Warns for UnsupportedFirst, Supported
        obj.DoesNotWorkOnWindows(); // This call site is reachable on: 'windows' 10.0.2000 and before. 'C.DoesNotWorkOnWindows()' is supported on: 'windows' 10.0.1903 and later.
    }

    [UnsupportedOSPlatform(""windows"")]
    [SupportedOSPlatform(""windows10.1"")]
    [UnsupportedOSPlatform(""windows10.0.2003"")]
    public void SameSupportForWindowsNotWarn()
    {
        obj.DoesNotWorkOnWindows();
    }
    
    public void AllSupportedWarnForAll()
    {
        obj.DoesNotWorkOnWindows(); // This call site is reachable on all platforms. 'C.DoesNotWorkOnWindows()' is supported on: 'windows' from version 10.0.1903 to 10.0.2004.
    }

    [SupportedOSPlatform(""windows10.0.2000"")]
    public void SupportedFromWindows10_0_2000()
    {
        // Should warn for [UnsupportedOSPlatform]
        obj.DoesNotWorkOnWindows(); // This call site is reachable on: 'windows' 10.0.2000 and later. 'C.DoesNotWorkOnWindows()' is unsupported on: 'windows' 10.0.2004 and later.
    }

    [SupportedOSPlatform(""windows10.0.1904"")]
    [UnsupportedOSPlatform(""windows10.0.1909"")]
    public void SupportedWindowsFrom10_0_1904_To10_0_1909_NotWarn()
    {
        // Should not warn
        obj.DoesNotWorkOnWindows();
    }
}

public class C
{
    [UnsupportedOSPlatform(""windows"")]
    [SupportedOSPlatform(""windows10.0.1903"")]
    [UnsupportedOSPlatform(""windows10.0.2004"")]
    public void DoesNotWorkOnWindows()
    {
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsReachable).WithLocation(18, 9).
                WithArguments("C.DoesNotWorkOnWindows()", "'windows' 10.0.1903 and later", "'windows' 10.0.2000 and before"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(31, 9).
                WithArguments("C.DoesNotWorkOnWindows()", "'windows' from version 10.0.1903 to 10.0.2004", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(38, 9).
                WithArguments("C.DoesNotWorkOnWindows()", "'windows' 10.0.2004 and later", "'windows' 10.0.2000 and later"));
        }

        [Fact]
        public async Task MultipleAttrbiutesSupportedOnlyWindowsListTest()
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
    public void WindowsOnlyUnsupportedFrom2004()
    {
    }
}
" + MockAttributesCsSource;
            await VerifyAnalyzerAsyncCs(source,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(0).
                WithArguments("C.WindowsOnlyUnsupportedFrom2004()", "'windows' 10.0.2004 and before", "'Linux'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(1).
                WithArguments("C.WindowsOnlyUnsupportedFrom2004()", "'windows' 10.0.2004 and before", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(2).
                WithArguments("C.WindowsOnlyUnsupportedFrom2004()", "'windows' 10.0.2004 and later", "'windows' 10.0.2000 and later"));
        }

        [Fact]
        public async Task CallSiteSupportedUnupportedNoMsBuildOptions()
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
" + TargetTypesForTest + MockAttributesCsSource;

            await VerifyAnalyzerAsyncCs(source,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(0).
                WithArguments("TypeWithoutAttributes.FunctionSupportedOnWindows()", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(1).
                WithArguments("TypeWithoutAttributes.FunctionSupportedOnWindows10()", "'windows' 10.0 and later", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(2).
                WithArguments("TypeSupportedOnWindows", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(3).
                WithArguments("TypeSupportedOnWindows.FunctionSupportedOnBrowser()", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(4).
                WithArguments("TypeSupportedOnWindows.FunctionSupportedOnWindows11AndBrowser()", "'windows' 11.0 and later", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(5).
                WithArguments("TypeSupportedOnBrowser", "'browser'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(6).
                WithArguments("TypeSupportedOnWindows10.FunctionSupportedOnBrowser()", "'windows' 10.0 and later", ""));
        }

        [Fact]
        public async Task CallSiteSupportedUnupporteWithMsBuildOptions()
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
" + TargetTypesForTest + MockAttributesCsSource;

            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(0).
                WithArguments("TypeUnsupportedOnBrowser", "'browser'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(1).
                WithArguments("TypeUnsupportedOnWindows10", "'windows' 10.0 and later", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(2).
                WithArguments("TypeUnsupportedOnWindows10.FunctionUnsupportedOnWindows11()", "'windows' 10.0 and later", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(3).
                WithArguments("TypeWithoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11()", "'windows' 11.0 and later", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(4).
                WithArguments("TypeWithoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12()", "'windows' from version 11.0 to 12.0", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(5).
                WithArguments("TypeWithoutAttributes.FunctionUnsupportedOnWindowsSupportedOnWindows11UnsupportedOnWindows12SupportedOnWindows13()", "'windows' from version 11.0 to 12.0", ""),
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
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(12).
                WithArguments("TypeUnsupportedOnWindowsSupportedOnWindows11", "'windows' 11.0 and later", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(13).
                WithArguments("TypeUnsupportedOnWindowsSupportedOnWindows11.FunctionUnsupportedOnWindows12()", "'windows' 11.0 and later", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(14).
                WithArguments("TypeUnsupportedOnWindowsSupportedOnWindows11.FunctionUnsupportedOnWindows12SupportedOnWindows13()", "'windows' 11.0 and later", ""));
        }

        [Fact]
        public async Task CallSiteCrossPlatform_CallsSupportedUnsupported_VersionedVersionlessAPIs()
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
" + AllowDenyListTestClasses + MockAttributesCsSource;

            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(0).WithArguments("DenyList.UnsupportedWindows()", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsAllPlatforms).WithLocation(1).WithArguments("DenyList.UnsupportedWindows10()", "'windows' 10.0 and later", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsAllPlatforms).WithLocation(2).WithArguments("DenyList.UnsupportedSupportedWindows8To10()", "'windows' from version 8.0 to 10.0", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(3).WithArguments("AllowList.WindowsOnly()", "'windows'", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(4).WithArguments("AllowList.Windows10Only()", "'windows' 10.0 and later", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(5).WithArguments("AllowList.WindowsOnlyUnsupportedFrom10()", "'windows' 10.0 and before", ""),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsAllPlatforms).WithLocation(6).WithArguments("AllowList.Windows10OnlyUnsupportedFrom11()", "'windows' from version 10.0 to 11.0", ""));
        }

        [Fact]
        public async Task CallSiteWindowsOnly_CallsSupportedUnsupported_VersionedVersionlessAPIs()
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
" + AllowDenyListTestClasses + MockAttributesCsSource;

            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0).WithArguments("DenyList.UnsupportedWindows()", "'windows'", "'windows'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(1).WithArguments("DenyList.UnsupportedWindows10()", "'windows' 10.0 and later", "'windows' all versions"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsReachable).WithLocation(2).WithArguments("DenyList.UnsupportedSupportedWindows8To10()", "'windows' from version 8.0 to 10.0", "'windows' all versions"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(3).WithArguments("AllowList.Windows10Only()", "'windows' 10.0 and later", "'windows' all versions"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(4).WithArguments("AllowList.WindowsOnlyUnsupportedFrom10()", "'windows' 10.0 and later", "'windows' all versions"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(5).WithArguments("AllowList.Windows10OnlyUnsupportedFrom11()", "'windows' from version 10.0 to 11.0", "'windows' all versions"));
        }

        [Fact]
        public async Task CallSiteWindows9Only_CallsSupportedUnsupported_VersionedVersionlessAPIs()
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
" + AllowDenyListTestClasses + MockAttributesCsSource;

            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0).WithArguments("DenyList.UnsupportedWindows()", "'windows' all versions", "'windows' 9.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(1).WithArguments("DenyList.UnsupportedWindows10()", "'windows' 10.0 and later", "'windows' 9.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(2).WithArguments("DenyList.UnsupportedSupportedWindows8To10()", "'windows' 10.0 and later", "'windows' 9.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(3).WithArguments("AllowList.Windows10Only()", "'windows' 10.0 and later", "'windows' 9.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(4).WithArguments("AllowList.WindowsOnlyUnsupportedFrom10()", "'windows' 10.0 and later", "'windows' 9.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(5).WithArguments("AllowList.Windows10OnlyUnsupportedFrom11()", "'windows' from version 10.0 to 11.0", "'windows' 9.0 and later"));
        }

        [Fact]
        public async Task CallSiteAllowList_CallsSupportedUnsupportedVersionedVersionlessAPIs()
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
        {|#1:DenyList.UnsupportedSupportedWindows8To10()|}; // This call site is reachable on: 'windows' 10.0 and before. 'DenyList.UnsupportedSupportedWindows8To10()' is supported on: 'windows' 8.0 and later.
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
" + AllowDenyListTestClasses + MockAttributesCsSource;

            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(0).WithArguments("DenyList.UnsupportedWindows()", "'windows' all versions", "'windows' 10.0 and before"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.SupportedCsReachable).WithLocation(1).WithArguments("DenyList.UnsupportedSupportedWindows8To10()", "'windows' 8.0 and later", "'windows' 10.0 and before"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(2).WithArguments("AllowList.Windows10Only()", "'windows' 10.0 and later", "'windows' 10.0 and before"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsReachable).WithLocation(3).WithArguments("AllowList.Windows10OnlyUnsupportedFrom11()", "'windows' from version 10.0 to 11.0", "'windows' 10.0 and before"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(4).WithArguments("DenyList.UnsupportedWindows()", "'windows' all versions", "'windows' from version 10.0 to 11.0"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(5).WithArguments("DenyList.UnsupportedWindows10()", "'windows' 10.0 and later", "'windows' from version 10.0 to 11.0"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(6).WithArguments("DenyList.UnsupportedSupportedWindows8To10()", "'windows' 10.0 and later", "'windows' from version 10.0 to 11.0"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.UnsupportedCsReachable).WithLocation(7).WithArguments("AllowList.WindowsOnlyUnsupportedFrom10()", "'windows' 10.0 and later", "'windows' from version 10.0 to 11.0"));
        }

        [Fact]
        public async Task CallSiteUnsupportedWindows_CallsSupportedUnsupported_VersionedVersionlessAPIs()
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
" + AllowDenyListTestClasses + MockAttributesCsSource;

            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsUnreachable).WithLocation(0).WithArguments("AllowList.WindowsOnly()", "'windows'", "'windows'"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsUnreachable).WithLocation(1).WithArguments("AllowList.Windows10Only()", "'windows' 10.0 and later", "'windows' all versions"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsUnreachable).WithLocation(2).WithArguments("AllowList.WindowsOnlyUnsupportedFrom10()", "'windows' 10.0 and before", "'windows' all versions"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsUnreachable).WithLocation(3).WithArguments("AllowList.Windows10OnlyUnsupportedFrom11()", "'windows' from version 10.0 to 11.0", "'windows' all versions"));
        }

        [Fact]
        public async Task CallSiteUnsupportsWindows9_CallsSupportedUnsupported_VersionedVersionlessAPIs()
        {
            var source = @"
 using System.Runtime.Versioning;
 
public class Test
{
    [UnsupportedOSPlatform(""windows9.0"")]
    public void UnsupportedWindows9Test()
    {
        DenyList.UnsupportedWindows(); // TODO fix come later: This call site is reachable on: 'windows' 9.0 and before. 'DenyList.UnsupportedWindows()' is unsupported on: 'windows' all versions.
        DenyList.UnsupportedWindows10();
        DenyList.UnsupportedSupportedWindows8To10(); // This call site is reachable on: 'windows' 9.0 and before. 'DenyList.UnsupportedSupportedWindows8To10()' is unsupported on: 'windows' 8.0 and before.
        {|#0:AllowList.WindowsOnly()|}; // This call site is unreachable on: 'windows' 9.0 and later. 'AllowList.WindowsOnly()' is only supported on: 'windows' all versions.
        {|#1:AllowList.Windows10Only()|}; // This call site is unreachable on: 'windows' 9.0 and later. 'AllowList.Windows10Only()' is only supported on: 'windows' 10.0 and later.
        {|#2:AllowList.WindowsOnlyUnsupportedFrom10()|}; // This call site is unreachable on: 'windows' 9.0 and later. 'AllowList.WindowsOnlyUnsupportedFrom10()' is only supported on: 'windows' 10.0 and before.
        {|#3:AllowList.Windows10OnlyUnsupportedFrom11()|}; // This call site is unreachable on: 'windows' 9.0 and later. 'AllowList.Windows10OnlyUnsupportedFrom11()' is only supported on: 'windows' from version 10.0 to 11.0.
    }
}
" + AllowDenyListTestClasses + MockAttributesCsSource;

            await VerifyAnalyzerAsyncCs(source, s_msBuildPlatforms,
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsUnreachable).WithLocation(0).WithArguments("AllowList.WindowsOnly()", "'windows' all versions", "'windows' 9.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsUnreachable).WithLocation(1).WithArguments("AllowList.Windows10Only()", "'windows' 10.0 and later", "'windows' 9.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsUnreachable).WithLocation(2).WithArguments("AllowList.WindowsOnlyUnsupportedFrom10()", "'windows' 10.0 and before", "'windows' 9.0 and later"),
                VerifyCS.Diagnostic(PlatformCompatibilityAnalyzer.OnlySupportedCsUnreachable).WithLocation(3).WithArguments("AllowList.Windows10OnlyUnsupportedFrom11()", "'windows' from version 10.0 to 11.0", "'windows' 9.0 and later"));
        }

        private static VerifyCS.Test PopulateTestCs(string sourceCode, params DiagnosticResult[] expected)
        {
            var test = new VerifyCS.Test
            {
                TestCode = sourceCode,
                ReferenceAssemblies = ReferenceAssemblies.NetCore.NetCoreApp50,
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
                TestState = { }
            };
            test.ExpectedDiagnostics.AddRange(expected);
            return test;
        }

        private static async Task VerifyAnalyzerAsyncCs(string sourceCode, params DiagnosticResult[] expectedDiagnostics)
            => await VerifyAnalyzerAsyncCs(sourceCode, "build_property.TargetFramework = net5", expectedDiagnostics);

        private static async Task VerifyAnalyzerAsyncCs(string sourceCode, string editorconfigText, params DiagnosticResult[] expectedDiagnostics)
        {
            var test = PopulateTestCs(sourceCode, expectedDiagnostics);
            test.TestState.AdditionalFiles.Add((".editorconfig", editorconfigText));
            await test.RunAsync();
        }

        private static async Task VerifyAnalyzerAsyncCs(string sourceCode, string editorconfigText)
        {
            var test = PopulateTestCs(sourceCode);
            test.TestState.AdditionalFiles.Add((".editorconfig", editorconfigText));
            await test.RunAsync();
        }

        private static async Task VerifyAnalyzerAsyncVb(string sourceCode, params DiagnosticResult[] expectedDiagnostics)
            => await VerifyAnalyzerAsyncVb(sourceCode, "build_property.TargetFramework = net5", expectedDiagnostics);

        private static async Task VerifyAnalyzerAsyncVb(string sourceCode, string editorconfigText, params DiagnosticResult[] expectedDiagnostics)
        {
            var test = PopulateTestVb(sourceCode, expectedDiagnostics);
            test.TestState.AdditionalFiles.Add((".editorconfig", editorconfigText));
            await test.RunAsync();
        }

        private static VerifyVB.Test PopulateTestVb(string sourceCode, params DiagnosticResult[] expected)
        {
            var test = new VerifyVB.Test
            {
                TestCode = sourceCode,
                ReferenceAssemblies = ReferenceAssemblies.NetCore.NetCoreApp50,
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

        private readonly string MockAttributesCsSource = @"
namespace System.Runtime.Versioning
{
    public abstract class OSPlatformAttribute : Attribute
    {
        private protected OSPlatformAttribute(string platformName)
        {
            PlatformName = platformName;
        }

        public string PlatformName { get; }
    }

    [AttributeUsage(AttributeTargets.Assembly,
                    AllowMultiple = false, Inherited = false)]
    public sealed class TargetPlatformAttribute : OSPlatformAttribute
    {
        public TargetPlatformAttribute(string platformName) : base(platformName)
        { }
    }

    [AttributeUsage(AttributeTargets.Assembly |
                    AttributeTargets.Class |
                    AttributeTargets.Constructor |
                    AttributeTargets.Event |
                    AttributeTargets.Method |
                    AttributeTargets.Module |
                    AttributeTargets.Property |
                    AttributeTargets.Field |
                    AttributeTargets.Struct,
                    AllowMultiple = true, Inherited = false)]
    public sealed class SupportedOSPlatformAttribute : OSPlatformAttribute
    {
        public SupportedOSPlatformAttribute(string platformName) : base(platformName)
        { }
    }

    [AttributeUsage(AttributeTargets.Assembly |
                    AttributeTargets.Class |
                    AttributeTargets.Constructor |
                    AttributeTargets.Event |
                    AttributeTargets.Method |
                    AttributeTargets.Module |
                    AttributeTargets.Property |
                    AttributeTargets.Field |
                    AttributeTargets.Struct,
                    AllowMultiple = true, Inherited = false)]
    public sealed class UnsupportedOSPlatformAttribute : OSPlatformAttribute
    {
        public UnsupportedOSPlatformAttribute(string platformName) : base(platformName)
        { }
    }
}
";

        private readonly string MockAttributesVbSource = @"
Namespace System.Runtime.Versioning
    Public MustInherit Class OSPlatformAttribute
        Inherits Attribute

        Private Protected Sub New(ByVal platformName As String)
            PlatformName = platformName
        End Sub

        Public ReadOnly Property PlatformName As String
    End Class

    <AttributeUsage(AttributeTargets.Assembly, AllowMultiple:=False, Inherited:=False)>
    Public NotInheritable Class TargetPlatformAttribute
        Inherits OSPlatformAttribute

        Public Sub New(ByVal platformName As String)
            MyBase.New(platformName)
        End Sub
    End Class

    <AttributeUsage(AttributeTargets.Assembly Or AttributeTargets.[Class] Or AttributeTargets.Constructor Or AttributeTargets.[Event] Or AttributeTargets.Method Or AttributeTargets.[Module] Or AttributeTargets.[Property] Or AttributeTargets.Field Or AttributeTargets.Struct, AllowMultiple:=True, Inherited:=False)>
    Public NotInheritable Class SupportedOSPlatformAttribute
        Inherits OSPlatformAttribute

        Public Sub New(ByVal platformName As String)
            MyBase.New(platformName)
        End Sub
    End Class

    <AttributeUsage(AttributeTargets.Assembly Or AttributeTargets.[Class] Or AttributeTargets.Constructor Or AttributeTargets.[Event] Or AttributeTargets.Method Or AttributeTargets.[Module] Or AttributeTargets.[Property] Or AttributeTargets.Field Or AttributeTargets.Struct, AllowMultiple:=True, Inherited:=False)>
    Public NotInheritable Class UnsupportedOSPlatformAttribute
        Inherits OSPlatformAttribute

        Public Sub New(ByVal platformName As String)
            MyBase.New(platformName)
        End Sub
    End Class
End Namespace
";
    }
}
