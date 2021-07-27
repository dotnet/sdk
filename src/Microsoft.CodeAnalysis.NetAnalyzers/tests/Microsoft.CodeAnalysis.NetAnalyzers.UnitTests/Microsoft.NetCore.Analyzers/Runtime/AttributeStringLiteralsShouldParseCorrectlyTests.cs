// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AttributeStringLiteralsShouldParseCorrectlyAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AttributeStringLiteralsShouldParseCorrectlyAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class AttributeStringLiteralsShouldParseCorrectlyTests
    {
        [Fact]
        public async Task CA2243_BadAttributeStringLiterals_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public sealed class BadAttributeStringLiterals
{
    private sealed class MyLiteralsAttribute : Attribute
    {
        private string m_url;
        private string m_version;
        private string m_guid;
        public MyLiteralsAttribute() { }
        public MyLiteralsAttribute(string url) { m_url = url; }
        public MyLiteralsAttribute(string url, int dummy1, string thisIsAVersion, int dummy2)
        {
            m_url = url;
            m_version = thisIsAVersion;
            if (dummy1 > dummy2) // just random stuff to use these arguments
                m_version = """";
        }
        public string Url { get { return m_url; } set { m_url = value; } }
        public string Version { get { return m_version; } set { m_version = value; } }
        public string GUID { get { return m_guid; } set { m_guid = value; } }
    }

    [MyLiterals(GUID = ""bad-guid"")]
    private int x;
    public BadAttributeStringLiterals() { DoNothing(1); }

    [MyLiterals(Url = ""bad url"", Version = ""helloworld"")]
    private void DoNothing(
    [MyLiterals(""bad url"")] int y)
    { if (x > 0) DoNothing2(y); }

    [MyLiterals(""good/url"", 5, ""1.0.bad"", 5)]
    private void DoNothing2(int y) { this.x = y; }
}",
CA2243CSharpDefaultResultAt(25, 6, "BadAttributeStringLiterals.MyLiteralsAttribute", "BadAttributeStringLiterals.MyLiteralsAttribute.GUID", "bad-guid", "Guid"),
CA2243CSharpDefaultResultAt(29, 6, "BadAttributeStringLiterals.MyLiteralsAttribute", "BadAttributeStringLiterals.MyLiteralsAttribute.Url", "bad url", "Uri"),
CA2243CSharpDefaultResultAt(31, 6, "BadAttributeStringLiterals.MyLiteralsAttribute", "url", "bad url", "Uri"));
        }

        [Fact]
        public async Task CA2243_BadGuids_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[GuidAttribute(GUID = ""bad-guid"")]
public class ClassWithBadlyFormattedNamedArgumentGuid
{
}

[GuidAttribute(GUID = ""59C91206-1BE9-4c54-A7EC-1941387E32DC-AFDF"")]
public class ClassWithTooManyDashesNamedArgumentGuid
{
}

[GuidAttribute(GUID = ""{0xCA761232, 0xED421111111, 0x11CE, {0xBA, 0xCD, 0x00, 0xAA, 0x00, 0x57, 0xB2, 0x11}}"")]
public class ClassWithOverflowNamedArgumentGuid
{
}

[GuidAttribute(GUID = """")]
public class ClassWithEmptyNamedArgumentGuid
{
}

[GuidAttribute(""bad-guid"")]
public class ClassWithBadlyFormattedRequiredArgumentGuid
{
}

[GuidAttribute(""59C91206-1BE9-4c54-A7EC-1941387E32DC-AFDF"")]
public class ClassWithTooManyDashesRequiredArgumentGuid
{
}

[GuidAttribute(""{0xCA761232, 0xED42, 0x11CE, {0xBA, 0xCD, 0x00, 0xAA, 0x00, 0x57, 0xB2, 0x99999}}"")]
public class ClassWithOverflowRequiredArgumentGuid
{
}

[GuidAttribute("""")]
public class ClassWithEmptyRequiredArgumentGuid
{
}

[AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
public sealed class GuidAttribute : Attribute
{
    private string m_guid;

    public GuidAttribute()
    {
    }

    public GuidAttribute(string ThisIsAGuid)
    {
        m_guid = ThisIsAGuid;
    }

    public string GUID
    {
        get { return m_guid; }
        set { m_guid = value; }
    }
}
",
CA2243CSharpDefaultResultAt(4, 2, "GuidAttribute", "GuidAttribute.GUID", "bad-guid", "Guid"),
CA2243CSharpDefaultResultAt(9, 2, "GuidAttribute", "GuidAttribute.GUID", "59C91206-1BE9-4c54-A7EC-1941387E32DC-AFDF", "Guid"),
CA2243CSharpDefaultResultAt(14, 2, "GuidAttribute", "GuidAttribute.GUID", "{0xCA761232, 0xED421111111, 0x11CE, {0xBA, 0xCD, 0x00, 0xAA, 0x00, 0x57, 0xB2, 0x11}}", "Guid"),
CA2243CSharpEmptyResultAt(19, 2, "GuidAttribute", "GuidAttribute.GUID", "Guid"),
CA2243CSharpDefaultResultAt(24, 2, "GuidAttribute", "ThisIsAGuid", "bad-guid", "Guid"),
CA2243CSharpDefaultResultAt(29, 2, "GuidAttribute", "ThisIsAGuid", "59C91206-1BE9-4c54-A7EC-1941387E32DC-AFDF", "Guid"),
CA2243CSharpDefaultResultAt(34, 2, "GuidAttribute", "ThisIsAGuid", "{0xCA761232, 0xED42, 0x11CE, {0xBA, 0xCD, 0x00, 0xAA, 0x00, 0x57, 0xB2, 0x99999}}", "Guid"),
CA2243CSharpEmptyResultAt(39, 2, "GuidAttribute", "ThisIsAGuid", "Guid"));
        }

        [Fact]
        public async Task CA2243_MiscSymbolsWithBadGuid_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[assembly: GuidAttribute(GUID = ""bad-guid"")]

public delegate void MiscDelegate([GuidAttribute(GUID = ""bad-guid"")] int p);

public class MiscClass<[GuidAttribute(GUID = ""bad-guid"")] U>
{
    public MiscClass<U> this[[GuidAttribute(GUID = ""bad-guid"")] int index]
    {
        get
        {
            return null;
        }
        set
        {

        }
    }
    public void M<[GuidAttribute(GUID = ""bad-guid"")] T>()
    {

    }
}

[AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
public sealed class GuidAttribute : Attribute
{
    private string m_guid;

    public GuidAttribute()
    {
    }

    public GuidAttribute(string ThisIsAGuid)
    {
        m_guid = ThisIsAGuid;
    }

    public string GUID
    {
        get { return m_guid; }
        set { m_guid = value; }
    }
}
",
CA2243CSharpDefaultResultAt(4, 12, "GuidAttribute", "GuidAttribute.GUID", "bad-guid", "Guid"),
CA2243CSharpDefaultResultAt(6, 36, "GuidAttribute", "GuidAttribute.GUID", "bad-guid", "Guid"),
CA2243CSharpDefaultResultAt(8, 25, "GuidAttribute", "GuidAttribute.GUID", "bad-guid", "Guid"),
CA2243CSharpDefaultResultAt(10, 31, "GuidAttribute", "GuidAttribute.GUID", "bad-guid", "Guid"),
CA2243CSharpDefaultResultAt(21, 20, "GuidAttribute", "GuidAttribute.GUID", "bad-guid", "Guid"));
        }

        [Fact]
        public async Task CA2243_NoDiagnostics_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public sealed class GoodAttributeStringLiterals
{
    private sealed class MyLiteralsAttribute : Attribute
    {
        private string m_url;
        private string m_version;
        private string m_guid;
        private int m_notAVersion;
        public MyLiteralsAttribute() { }
        public MyLiteralsAttribute(string url) { m_url = url; }
        public MyLiteralsAttribute(string url, int dummy1, string thisIsAVersion, int notAVersion)
        {
            m_url = url;
            m_version = thisIsAVersion;
            m_notAVersion = notAVersion + dummy1;
        }
        public string Url { get { return m_url; } set { m_url = value; } }
        public string Version { get { return m_version; } set { m_version = value; } }
        public string GUID { get { return m_guid; } set { m_guid = value; } }
        public int NotAVersion { get { return m_notAVersion; } set { m_notAVersion = value; } }
    }

    [MyLiterals(""good/relative/url"", GUID = ""8fcd093bc1058acf8fcd093bc1058acf"", Version = ""1.4.325.12"")]
    private int x;
    public GoodAttributeStringLiterals() { DoNothing(1); }

    [MyLiterals(GUID = ""{8fcd093b-c105-8acf-8fcd-093bc1058acf}"", Url = ""http://good/absolute/url.htm"")]
    private void DoNothing(
        [MyLiterals(""goodurl/"", NotAVersion = 12)] int y) { if (x > 0) DoNothing2(y); }
    [MyLiterals(""http://good/url"", 5, ""1.0.50823.98"", 5)]
    private void DoNothing2(int y) { this.x = y; }
}

[AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
public sealed class UriTemplateAttribute : Attribute
{
    private string m_uriTemplate;

    public UriTemplateAttribute()
    {
    }

    public UriTemplateAttribute(string uriTemplate)
    {
        m_uriTemplate = uriTemplate;
    }

    public string UriTemplate
    {
        get { return m_uriTemplate; }
        set { m_uriTemplate = value; }
    }
}

public static class ClassWithExceptionForUri
{
    [UriTemplate(UriTemplate=""{0}"")]
    public static void MethodWithInvalidUriNamedArgumentThatShouldBeIgnored()
    {
    }

    [UriTemplate(UriTemplate = """")]
    public static void MethodWithEmptyUriNamedArgumentThatShouldBeIgnored()
    {
    }

    [UriTemplate(""{0}"")]
    public static void MethodWithInvalidUriRequiredArgumentThatShouldBeIgnored()
    {
    }

    [UriTemplate("""")]
    public static void MethodWithEmptyUriRequiredArgumentThatShouldBeIgnored()
    {
    }
}");
        }

        [Fact]
        public async Task CA2243_BadAttributeStringLiterals_Basic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public NotInheritable Class BadAttributeStringLiterals
    <MyLiterals(GUID:=""bad-guid"")>
    Private x As Integer
    Public Sub New()
        DoNothing(1)
    End Sub

    <MyLiterals(Url:=""bad url"", Version:=""helloworld"")>
    Private Sub DoNothing(<MyLiterals(""bad url"")> y As Integer)
        If x > 0 Then
            DoNothing2(y)
        End If
    End Sub

    <MyLiterals(""good/url"", 5, ""1.0.bad"", 5)>
    Private Sub DoNothing2(y As Integer)
        Me.x = y
    End Sub

    Private NotInheritable Class MyLiteralsAttribute
        Inherits Attribute
        Private m_url As String
        Private m_version As String
        Private m_guid As String
        Public Sub New()
        End Sub
        Public Sub New(url As String)
            m_url = url
        End Sub
        Public Sub New(url As String, dummy1 As Integer, thisIsAVersion As String, dummy2 As Integer)
            m_url = url
            m_version = thisIsAVersion
            If dummy1 > dummy2 Then
                ' just random stuff to use these arguments
                m_version = """"
            End If
        End Sub
        Public Property Url() As String
            Get
                Return m_url
            End Get
            Set
                m_url = value
            End Set
        End Property
        Public Property Version() As String
            Get
                Return m_version
            End Get
            Set
                m_version = value
            End Set
        End Property
        Public Property GUID() As String
            Get
                Return m_guid
            End Get
            Set
                m_guid = value
            End Set
        End Property
    End Class
End Class",
CA2243BasicDefaultResultAt(5, 6, "BadAttributeStringLiterals.MyLiteralsAttribute", "BadAttributeStringLiterals.MyLiteralsAttribute.GUID", "bad-guid", "Guid"),
CA2243BasicDefaultResultAt(11, 6, "BadAttributeStringLiterals.MyLiteralsAttribute", "BadAttributeStringLiterals.MyLiteralsAttribute.Url", "bad url", "Uri"),
CA2243BasicDefaultResultAt(12, 28, "BadAttributeStringLiterals.MyLiteralsAttribute", "url", "bad url", "Uri"));
        }

        [Fact]
        public async Task CA2243_BadGuids_Basic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

<GuidAttribute(GUID := ""bad-guid"")> _
Public Class ClassWithBadlyFormattedNamedArgumentGuid
End Class

<GuidAttribute(GUID := ""59C91206-1BE9-4c54-A7EC-1941387E32DC-AFDF"")> _
Public Class ClassWithTooManyDashesNamedArgumentGuid
End Class

<GuidAttribute(GUID := ""{0xCA761232, 0xED421111111, 0x11CE, {0xBA, 0xCD, 0x00, 0xAA, 0x00, 0x57, 0xB2, 0x11}}"")> _
Public Class ClassWithOverflowNamedArgumentGuid
End Class

<GuidAttribute(GUID := """")> _
Public Class ClassWithEmptyNamedArgumentGuid
End Class

<GuidAttribute(""bad-guid"")> _
Public Class ClassWithBadlyFormattedRequiredArgumentGuid
End Class

<GuidAttribute(""59C91206-1BE9-4c54-A7EC-1941387E32DC-AFDF"")> _
Public Class ClassWithTooManyDashesRequiredArgumentGuid
End Class

<GuidAttribute(""{0xCA761232, 0xED42, 0x11CE, {0xBA, 0xCD, 0x00, 0xAA, 0x00, 0x57, 0xB2, 0x99999}}"")> _
Public Class ClassWithOverflowRequiredArgumentGuid
End Class

<GuidAttribute("""")> _
Public Class ClassWithEmptyRequiredArgumentGuid
End Class

<AttributeUsage(AttributeTargets.All, AllowMultiple := False)> _
Public NotInheritable Class GuidAttribute
    Inherits Attribute
    Private m_guid As String

    Public Sub New()
    End Sub

    Public Sub New(ThisIsAGuid As String)
        m_guid = ThisIsAGuid
    End Sub

    Public Property GUID() As String
        Get
            Return m_guid
        End Get
        Set
            m_guid = value
        End Set
    End Property
End Class
",
CA2243BasicDefaultResultAt(4, 2, "GuidAttribute", "GuidAttribute.GUID", "bad-guid", "Guid"),
CA2243BasicDefaultResultAt(8, 2, "GuidAttribute", "GuidAttribute.GUID", "59C91206-1BE9-4c54-A7EC-1941387E32DC-AFDF", "Guid"),
CA2243BasicDefaultResultAt(12, 2, "GuidAttribute", "GuidAttribute.GUID", "{0xCA761232, 0xED421111111, 0x11CE, {0xBA, 0xCD, 0x00, 0xAA, 0x00, 0x57, 0xB2, 0x11}}", "Guid"),
CA2243BasicEmptyResultAt(16, 2, "GuidAttribute", "GuidAttribute.GUID", "Guid"),
CA2243BasicDefaultResultAt(20, 2, "GuidAttribute", "ThisIsAGuid", "bad-guid", "Guid"),
CA2243BasicDefaultResultAt(24, 2, "GuidAttribute", "ThisIsAGuid", "59C91206-1BE9-4c54-A7EC-1941387E32DC-AFDF", "Guid"),
CA2243BasicDefaultResultAt(28, 2, "GuidAttribute", "ThisIsAGuid", "{0xCA761232, 0xED42, 0x11CE, {0xBA, 0xCD, 0x00, 0xAA, 0x00, 0x57, 0xB2, 0x99999}}", "Guid"),
CA2243BasicEmptyResultAt(32, 2, "GuidAttribute", "ThisIsAGuid", "Guid"));
        }

        [Fact]
        public async Task CA2243_MiscSymbolsWithBadGuid_Basic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

<Assembly: GuidAttribute(GUID := ""bad-guid"")>

Public Delegate Sub MiscDelegate(<GuidAttribute(GUID := ""bad-guid"")> p As Integer)

Public Class MiscClass
    Public Default Property Item(<GuidAttribute(GUID := ""bad-guid"")> index As Integer) As MiscClass
        Get
            Return Nothing
        End Get

        Set
        End Set
    End Property
End Class

<AttributeUsage(AttributeTargets.All, AllowMultiple := True)> _
Public NotInheritable Class GuidAttribute
    Inherits Attribute
    Private m_guid As String

    Public Sub New()
    End Sub

    Public Sub New(ThisIsAGuid As String)
        m_guid = ThisIsAGuid
    End Sub

    Public Property GUID() As String
        Get
            Return m_guid
        End Get
        Set
            m_guid = value
        End Set
    End Property
End Class
",
CA2243BasicDefaultResultAt(4, 2, "GuidAttribute", "GuidAttribute.GUID", "bad-guid", "Guid"),
CA2243BasicDefaultResultAt(6, 35, "GuidAttribute", "GuidAttribute.GUID", "bad-guid", "Guid"),
CA2243BasicDefaultResultAt(9, 35, "GuidAttribute", "GuidAttribute.GUID", "bad-guid", "Guid"));
        }

        [Fact]
        public async Task CA2243_NoDiagnostics_Basic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public NotInheritable Class GoodAttributeStringLiterals
    Private NotInheritable Class MyLiteralsAttribute
        Inherits Attribute
        Private m_url As String
        Private m_version As String
        Private m_guid As String
        Private m_notAVersion As Integer
        Public Sub New()
        End Sub
        Public Sub New(url As String)
            m_url = url
        End Sub
        Public Sub New(url As String, dummy1 As Integer, thisIsAVersion As String, notAVersion As Integer)
            m_url = url
            m_version = thisIsAVersion
            m_notAVersion = notAVersion + dummy1
        End Sub
        Public Property Url() As String
            Get
                Return m_url
            End Get
            Set
                m_url = Value
            End Set
        End Property
        Public Property Version() As String
            Get
                Return m_version
            End Get
            Set
                m_version = Value
            End Set
        End Property
        Public Property GUID() As String
            Get
                Return m_guid
            End Get
            Set
                m_guid = Value
            End Set
        End Property
        Public Property NotAVersion() As Integer
            Get
                Return m_notAVersion
            End Get
            Set
                m_notAVersion = Value
            End Set
        End Property
    End Class

    <MyLiterals(""good/relative/url"", GUID:=""8fcd093bc1058acf8fcd093bc1058acf"", Version:=""1.4.325.12"")>
    Private x As Integer
    Public Sub New()
        DoNothing(1)
    End Sub

    <MyLiterals(GUID:=""{8fcd093b-c105-8acf-8fcd-093bc1058acf}"", Url:=""http://good/absolute/url.htm"")>
    Private Sub DoNothing(<MyLiterals(""goodurl/"", NotAVersion:=12)> y As Integer)
        If x > 0 Then
            DoNothing2(y)
        End If
    End Sub
    <MyLiterals(""http://good/url"", 5, ""1.0.50823.98"", 5)>
    Private Sub DoNothing2(y As Integer)
        Me.x = y
    End Sub
End Class

<AttributeUsage(AttributeTargets.All, AllowMultiple:=False)>
Public NotInheritable Class UriTemplateAttribute
    Inherits Attribute
    Private m_uriTemplate As String

    Public Sub New()
    End Sub

    Public Sub New(uriTemplate As String)
        m_uriTemplate = uriTemplate
    End Sub

    Public Property UriTemplate() As String
        Get
            Return m_uriTemplate
        End Get
        Set
            m_uriTemplate = Value
        End Set
    End Property
End Class

Public NotInheritable Class ClassWithExceptionForUri
    Private Sub New()
    End Sub
    <UriTemplate(UriTemplate:=""{0}"")>
    Public Shared Sub MethodWithInvalidUriNamedArgumentThatShouldBeIgnored()
    End Sub

    <UriTemplate(UriTemplate:="""")>
    Public Shared Sub MethodWithEmptyUriNamedArgumentThatShouldBeIgnored()
    End Sub

    <UriTemplate(""{0}"")>
    Public Shared Sub MethodWithInvalidUriRequiredArgumentThatShouldBeIgnored()
    End Sub

    <UriTemplate("""")>
    Public Shared Sub MethodWithEmptyUriRequiredArgumentThatShouldBeIgnored()
    End Sub
End Class
");
        }

        [Fact, WorkItem(3635, "https://github.com/dotnet/roslyn-analyzers/issues/3635")]
        public async Task ObsoleteAttributeUrlFormat_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace System
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
    public class ObsoleteAttribute : Attribute
    {
        public string UrlFormat { get; set; }
    }
}

namespace Something
{
    using System;

    public class C
    {
        [Obsolete(UrlFormat = """")]
        public void M1() {}

        [Obsolete(UrlFormat = ""{0}"")]
        public void M2() {}
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Namespace System
    <AttributeUsage(AttributeTargets.All, AllowMultiple:=False)>
    Public Class ObsoleteAttribute
        Inherits Attribute

        Public Property UrlFormat As String
    End Class
End Namespace

Namespace Something
    Public Class C
        <Obsolete(UrlFormat:="""")>
        Public Sub M1()
        End Sub

        <Obsolete(UrlFormat:=""{0}"")>
        Public Sub M2()
        End Sub
    End Class
End Namespace");
        }

        private DiagnosticResult CA2243CSharpDefaultResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyCS.Diagnostic(AttributeStringLiteralsShouldParseCorrectlyAnalyzer.DefaultRule)
               .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
               .WithArguments(arguments);

        private DiagnosticResult CA2243BasicDefaultResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(AttributeStringLiteralsShouldParseCorrectlyAnalyzer.DefaultRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);

        private DiagnosticResult CA2243CSharpEmptyResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyCS.Diagnostic(AttributeStringLiteralsShouldParseCorrectlyAnalyzer.EmptyRule)
               .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
               .WithArguments(arguments);

        private DiagnosticResult CA2243BasicEmptyResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(AttributeStringLiteralsShouldParseCorrectlyAnalyzer.EmptyRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}