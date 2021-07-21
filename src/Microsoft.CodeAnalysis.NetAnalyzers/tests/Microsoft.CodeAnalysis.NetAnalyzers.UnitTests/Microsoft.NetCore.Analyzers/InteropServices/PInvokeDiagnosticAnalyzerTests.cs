// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.PInvokeDiagnosticAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.PInvokeDiagnosticAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.InteropServices.UnitTests
{
    public class PInvokeDiagnosticAnalyzerTests
    {
        #region Verifiers

        private DiagnosticResult CSharpResult1401(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyCS.Diagnostic(PInvokeDiagnosticAnalyzer.RuleCA1401)
               .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
               .WithArguments(arguments);

        private DiagnosticResult BasicResult1401(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(PInvokeDiagnosticAnalyzer.RuleCA1401)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);

        private DiagnosticResult CSharpResult2101(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyCS.Diagnostic(PInvokeDiagnosticAnalyzer.RuleCA2101)
               .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
               .WithArguments(arguments);

        private DiagnosticResult BasicResult2101(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(PInvokeDiagnosticAnalyzer.RuleCA2101)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);

        #endregion

        #region CA1401 tests

        [Fact]
        public async Task CA1401CSharpTest()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

public class C
{
    [DllImport(""user32.dll"")]
    public static extern void Method1(); // should not be public

    [DllImport(""user32.dll"")]
    protected static extern void Method2(); // should not be protected

    [DllImport(""user32.dll"")]
    private static extern void Method3(); // private is OK

    [DllImport(""user32.dll"")]
    static extern void Method4(); // implicitly private is OK
}
",
                CSharpResult1401(7, 31, "Method1"),
                CSharpResult1401(10, 34, "Method2"));
        }

        [Fact]
        public async Task CA1401CSharpTestWithScope()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

public class C
{
    [DllImport(""user32.dll"")]
    public static extern void {|CA1401:Method1|}(); // should not be public

    [DllImport(""user32.dll"")]
    protected static extern void {|CA1401:Method2|}(); // should not be protected

    [DllImport(""user32.dll"")]
    private static extern void Method3(); // private is OK

    [DllImport(""user32.dll"")]
    static extern void Method4(); // implicitly private is OK
}
");
        }

        [Fact]
        public async Task CA1401BasicSubTest()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class C
    <DllImport(""user32.dll"")>
    Public Shared Sub Method1() ' should not be public
    End Sub

    <DllImport(""user32.dll"")>
    Protected Shared Sub Method2() ' should not be protected
    End Sub

    <DllImport(""user32.dll"")>
    Private Shared Sub Method3() ' private is OK
    End Sub

    <DllImport(""user32.dll"")>
    Shared Sub Method4() ' implicitly public is not OK
    End Sub
End Class
",
                BasicResult1401(6, 23, "Method1"),
                BasicResult1401(10, 26, "Method2"),
                BasicResult1401(18, 16, "Method4"));
        }

        [Fact]
        public async Task CA1401BasicSubTestWithScope()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class C
    <DllImport(""user32.dll"")>
    Public Shared Sub {|CA1401:Method1|}() ' should not be public
    End Sub

    <DllImport(""user32.dll"")>
    Protected Shared Sub {|CA1401:Method2|}() ' should not be protected
    End Sub

    <DllImport(""user32.dll"")>
    Private Shared Sub Method3() ' private is OK
    End Sub

    <DllImport(""user32.dll"")>
    Shared Sub {|CA1401:Method4|}() ' implicitly public is not OK
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1401BasicFunctionTest()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class C
    <DllImport(""user32.dll"")>
    Public Shared Function Method1() As Integer ' should not be public
    End Function

    <DllImport(""user32.dll"")>
    Protected Shared Function Method2() As Integer ' should not be protected
    End Function

    <DllImport(""user32.dll"")>
    Private Shared Function Method3() As Integer ' private is OK
    End Function

    <DllImport(""user32.dll"")>
    Shared Function Method4() As Integer ' implicitly public is not OK
    End Function
End Class
",
                BasicResult1401(6, 28, "Method1"),
                BasicResult1401(10, 31, "Method2"),
                BasicResult1401(18, 21, "Method4"));
        }

        [Fact]
        public async Task CA1401BasicDeclareSubTest()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class C
    Public Declare Sub Method1 Lib ""user32.dll"" Alias ""Method1"" () ' should not be public

    Protected Declare Sub Method2 Lib ""user32.dll"" Alias ""Method2"" () ' should not be protected

    Private Declare Sub Method3 Lib ""user32.dll"" Alias ""Method3"" () ' private is OK

    Declare Sub Method4 Lib ""user32.dll"" Alias ""Method4"" () ' implicitly public is not OK
End Class
",
                BasicResult1401(5, 24, "Method1"),
                BasicResult1401(7, 27, "Method2"),
                BasicResult1401(11, 17, "Method4"));
        }

        [Fact]
        public async Task CA1401BasicDeclareFunctionTest()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class C
    Public Declare Function Method1 Lib ""user32.dll"" Alias ""Method1"" () As Integer ' should not be public

    Protected Declare Function Method2 Lib ""user32.dll"" Alias ""Method2"" () As Integer ' should not be protected

    Private Declare Function Method3 Lib ""user32.dll"" Alias ""Method3"" () As Integer ' private is OK

    Declare Function Method4 Lib ""user32.dll"" Alias ""Method4"" () As Integer ' implicitly public is not OK
End Class
",
                BasicResult1401(5, 29, "Method1"),
                BasicResult1401(7, 32, "Method2"),
                BasicResult1401(11, 22, "Method4"));
        }

        [WorkItem(792, "https://github.com/dotnet/roslyn-analyzers/issues/792")]
        [Fact]
        public async Task CA1401CSharpNonPublic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

public sealed class TimerFontContainer
{
    private static class NativeMethods
    {
        [DllImport(""gdi32.dll"")]
        public static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, [In] ref uint pcFonts);
    }
}
");
        }

        [WorkItem(792, "https://github.com/dotnet/roslyn-analyzers/issues/792")]
        [Fact]
        public async Task CA1401BasicNonPublic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public NotInheritable Class TimerFontContainer
    Private Class NativeMethods
        Public Declare Function AddFontMemResourceEx Lib ""gdi32.dll"" (pbFont As Integer, cbFont As Integer, pdv As Integer) As Integer
    End Class
End Class
");
        }

        #endregion

        #region CA2101 tests

        [Fact]
        public async Task CA2101SimpleCSharpTest()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;
using System.Text;

class C
{
    [DllImport(""user32.dll"")]
    private static extern void Method1(string s); // one string parameter

    [DllImport(""user32.dll"")]
    private static extern void Method2(string s, string t); // two string parameters, should be only 1 diagnostic

    [DllImport(""user32.dll"")]
    private static extern void Method3(StringBuilder s); // one StringBuilder parameter

    [DllImport(""user32.dll"")]
    private static extern void Method4(StringBuilder s, StringBuilder t); // two StringBuilder parameters, should be only 1 diagnostic
}
",
                CSharpResult2101(7, 6),
                CSharpResult2101(10, 6),
                CSharpResult2101(13, 6),
                CSharpResult2101(16, 6));
        }

        [Fact]
        public async Task CA2101SimpleCSharpTestWithScope()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;
using System.Text;

class C
{
    [{|CA2101:DllImport(""user32.dll"")|}]
    private static extern void Method1(string s); // one string parameter

    [{|CA2101:DllImport(""user32.dll"")|}]
    private static extern void Method2(string s, string t); // two string parameters, should be only 1 diagnostic

    [{|CA2101:DllImport(""user32.dll"")|}]
    private static extern void Method3(StringBuilder s); // one StringBuilder parameter

    [{|CA2101:DllImport(""user32.dll"")|}]
    private static extern void Method4(StringBuilder s, StringBuilder t); // two StringBuilder parameters, should be only 1 diagnostic
}
");
        }

        [Fact]
        public async Task CA2101SimpleBasicTest()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices
Imports System.Text

Class C
    <DllImport(""user32.dll"")>
    Private Shared Sub Method1(s As String) ' one string parameter
    End Sub

    <DllImport(""user32.dll"")>
    Private Shared Sub Method2(s As String, t As String) ' two string parameters, should be only 1 diagnostic
    End Sub

    <DllImport(""user32.dll"")>
    Private Shared Sub Method3(s As StringBuilder) ' one StringBuilder parameter
    End Sub

    <DllImport(""user32.dll"")>
    Private Shared Sub Method4(s As StringBuilder, t As StringBuilder) ' two StringBuilder parameters, should be only 1 diagnostic
    End Sub
End Class
",
                BasicResult2101(6, 6),
                BasicResult2101(10, 6),
                BasicResult2101(14, 6),
                BasicResult2101(18, 6));
        }

        [Fact]
        public async Task CA2101SimpleBasicTestWithScope()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices
Imports System.Text

Class C
    <{|CA2101:DllImport(""user32.dll"")|}>
    Private Shared Sub Method1(s As String) ' one string parameter
    End Sub

    <{|CA2101:DllImport(""user32.dll"")|}>
    Private Shared Sub Method2(s As String, t As String) ' two string parameters, should be only 1 diagnostic
    End Sub

    <{|CA2101:DllImport(""user32.dll"")|}>
    Private Shared Sub Method3(s As StringBuilder) ' one StringBuilder parameter
    End Sub

    <{|CA2101:DllImport(""user32.dll"")|}>
    Private Shared Sub Method4(s As StringBuilder, t As StringBuilder) ' two StringBuilder parameters, should be only 1 diagnostic
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA2101SimpleDeclareBasicTest()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Text

Class C
    Private Declare Sub Method1 Lib ""user32.dll"" (s As String) ' one string parameter

    Private Declare Sub Method2 Lib ""user32.dll"" (s As String, t As String) ' two string parameters, should be only 1 diagnostic

    Private Declare Function Method3 Lib ""user32.dll"" (s As StringBuilder) As Integer ' one StringBuilder parameter

    Private Declare Function Method4 Lib ""user32.dll"" (s As StringBuilder, t As StringBuilder) As Integer ' two StringBuilder parameters, should be only 1 diagnostic
End Class
",
                BasicResult2101(5, 25),
                BasicResult2101(7, 25),
                BasicResult2101(9, 30),
                BasicResult2101(11, 30));
        }

        [Fact]
        public async Task CA2101ParameterMarshaledCSharpTest()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;
using System.Text;

class C
{
    [DllImport(""user32.dll"")]
    private static extern void Method1([MarshalAs(UnmanagedType.LPWStr)] string s); // marshaling specified on parameter

    [DllImport(""user32.dll"")]
    private static extern void Method2([MarshalAs(UnmanagedType.LPWStr)] StringBuilder s);

    [DllImport(""user32.dll"")]
    private static extern void Method3([MarshalAs(UnmanagedType.LPWStr)] string s, [MarshalAs(UnmanagedType.LPWStr)] string t);

    [DllImport(""user32.dll"")]
    private static extern void Method4([MarshalAs(UnmanagedType.LPWStr)] StringBuilder s, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder t);

    [DllImport(""user32.dll"")]
    private static extern void Method5([MarshalAs(UnmanagedType.LPWStr)] string s, string t); // un-marshaled second parameter

    [DllImport(""user32.dll"")]
    private static extern void Method6([MarshalAs(UnmanagedType.LPWStr)] StringBuilder s, StringBuilder t);

    [DllImport(""user32.dll"")]
    private static extern void Method7([MarshalAs(UnmanagedType.LPStr)] string s); // marshaled, but as the wrong type

    [DllImport(""user32.dll"")]
    private static extern void Method8([MarshalAs(UnmanagedType.LPStr)] StringBuilder s);

    [DllImport(""user32.dll"")]
    private static extern void Method9([MarshalAs(UnmanagedType.LPStr)] string s, [MarshalAs(UnmanagedType.LPStr)] string t); // two parameters marshaled as the wrong type

    [DllImport(""user32.dll"")]
    private static extern void Method10([MarshalAs(UnmanagedType.LPStr)] StringBuilder s, [MarshalAs(UnmanagedType.LPStr)] StringBuilder t);

    [DllImport(""user32.dll"")]
    private static extern void Method11([MarshalAs((short)0)] string s);
}
",
                CSharpResult2101(19, 6),
                CSharpResult2101(22, 6),
                CSharpResult2101(26, 41),
                CSharpResult2101(29, 41),
                CSharpResult2101(32, 41),
                CSharpResult2101(32, 84),
                CSharpResult2101(35, 42),
                CSharpResult2101(35, 92),
                CSharpResult2101(38, 42));
        }

        [Fact]
        public async Task CA2101ParameterMarshaledBasicTest()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices
Imports System.Text

Class C
    <DllImport(""user32.dll"")>
    Private Shared Sub Method1(<MarshalAs(UnmanagedType.LPWStr)> s As String) ' marshaling specified on parameter
    End Sub

    <DllImport(""user32.dll"")>
    Private Shared Sub Method2(<MarshalAs(UnmanagedType.LPWStr)> s As StringBuilder)
    End Sub

    <DllImport(""user32.dll"")>
    Private Shared Sub Method3(<MarshalAs(UnmanagedType.LPWStr)> s As String, <MarshalAs(UnmanagedType.LPWStr)> t As String)
    End Sub

    <DllImport(""user32.dll"")>
    Private Shared Sub Method4(<MarshalAs(UnmanagedType.LPWStr)> s As StringBuilder, <MarshalAs(UnmanagedType.LPWStr)> t As StringBuilder)
    End Sub

    <DllImport(""user32.dll"")>
    Private Shared Sub Method5(<MarshalAs(UnmanagedType.LPWStr)> s As String, t As String) ' un-marshaled second parameter
    End Sub

    <DllImport(""user32.dll"")>
    Private Shared Sub Method6(<MarshalAs(UnmanagedType.LPWStr)> s As StringBuilder, t As StringBuilder)
    End Sub

    <DllImport(""user32.dll"")>
    Private Shared Sub Method7(<MarshalAs(UnmanagedType.LPStr)> s As String) ' marshaled, but as the wrong type
    End Sub

    <DllImport(""user32.dll"")>
    Private Shared Sub Method8(<MarshalAs(UnmanagedType.LPStr)> s As StringBuilder)
    End Sub

    <DllImport(""user32.dll"")>
    Private Shared Sub Method9(<MarshalAs(UnmanagedType.LPStr)> s As String, <MarshalAs(UnmanagedType.LPStr)> t As String) ' two parameters marshaled as the wrong type
    End Sub

    <DllImport(""user32.dll"")>
    Private Shared Sub Method10(<MarshalAs(UnmanagedType.LPStr)> s As StringBuilder, <MarshalAs(UnmanagedType.LPStr)> t As StringBuilder)
    End Sub

    <DllImport(""user32.dll"")>
    Private Shared Sub Method11(<MarshalAs(CShort(0))> s As String)
    End Sub
End Class
",
                BasicResult2101(22, 6),
                BasicResult2101(26, 6),
                BasicResult2101(31, 33),
                BasicResult2101(35, 33),
                BasicResult2101(39, 33),
                BasicResult2101(39, 79),
                BasicResult2101(43, 34),
                BasicResult2101(43, 87),
                BasicResult2101(47, 34));
        }

        [Fact]
        public async Task CA2101CharSetCSharpTest()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;
using System.Text;

class C
{
    [DllImport(""user32.dll"", CharSet = CharSet.Auto)]
    private static extern void Method1(string s); // wrong marshaling

    [DllImport(""user32.dll"", CharSet = CharSet.Auto)]
    private static extern void Method2(StringBuilder s);

    [DllImport(""user32.dll"", CharSet = CharSet.Unicode)]
    private static extern void Method3(string s); // correct marshaling

    [DllImport(""user32.dll"", CharSet = CharSet.Unicode)]
    private static extern void Method4(StringBuilder s);

    [DllImport(""user32.dll"", CharSet = CharSet.Unicode)]
    private static extern void Method5([MarshalAs(UnmanagedType.LPStr)] string s); // correct marshaling on method, not on parameter

    [DllImport(""user32.dll"", CharSet = CharSet.Unicode)]
    private static extern void Method6([MarshalAs(UnmanagedType.LPStr)] StringBuilder s);
}
",
                CSharpResult2101(7, 6),
                CSharpResult2101(10, 6),
                CSharpResult2101(20, 41),
                CSharpResult2101(23, 41));
        }

        [Fact]
        public async Task CA2101CharSetBasicTest()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices
Imports System.Text

Class C
    <DllImport(""user32.dll"", CharSet := CharSet.Auto)>
    Private Shared Sub Method1(s As String) ' wrong marshaling
    End Sub

    <DllImport(""user32.dll"", CharSet := CharSet.Auto)>
    Private Shared Sub Method2(s As StringBuilder)
    End Sub

    <DllImport(""user32.dll"", CharSet := CharSet.Unicode)>
    Private Shared Sub Method3(s As String) ' correct marshaling
    End Sub

    <DllImport(""user32.dll"", CharSet := CharSet.Unicode)>
    Private Shared Sub Method4(s As StringBuilder)
    End Sub

    <DllImport(""user32.dll"", CharSet := CharSet.Unicode)>
    Private Shared Sub Method5(<MarshalAs(UnmanagedType.LPStr)> s As String) ' correct marshaling on method, not on parameter
    End Sub

    <DllImport(""user32.dll"", CharSet := CharSet.Unicode)>
    Private Shared Sub Method6(<MarshalAs(UnmanagedType.LPStr)> s As StringBuilder)
    End Sub
End Class
",
                BasicResult2101(6, 6),
                BasicResult2101(10, 6),
                BasicResult2101(23, 33),
                BasicResult2101(27, 33));
        }

        [Fact]
        public async Task CA2101ReturnTypeCSharpTest()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;
using System.Text;

class C
{
    [DllImport(""user32.dll"")]
    private static extern string Method1(); // wrong marshaling on return type

    [DllImport(""user32.dll"")]
    private static extern StringBuilder Method2();

    [DllImport(""user32.dll"", CharSet = CharSet.Unicode)]
    private static extern string Method3(); // correct marshaling on return type

    [DllImport(""user32.dll"", CharSet = CharSet.Unicode)]
    private static extern StringBuilder Method4();
}
",
                CSharpResult2101(7, 6),
                CSharpResult2101(10, 6));
        }

        [Fact]
        public async Task CA2101ReturnTypeBasicTest()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices
Imports System.Text

Class C
    <DllImport(""user32.dll"")>
    Private Shared Function Method1() As String ' wrong marshaling on return type
    End Function

    <DllImport(""user32.dll"")>
    Private Shared Function Method2() As StringBuilder
    End Function

    <DllImport(""user32.dll"", CharSet := CharSet.Unicode)>
    Private Shared Function Method3() As String ' correct marshaling on return type
    End Function

    <DllImport(""user32.dll"", CharSet := CharSet.Unicode)>
    Private Shared Function Method4() As StringBuilder
    End Function

    Private Declare Function Method5 Lib ""user32.dll"" () As String
End Class
",
                BasicResult2101(6, 6),
                BasicResult2101(10, 6),
                BasicResult2101(22, 30));
        }

        #endregion
    }
}
