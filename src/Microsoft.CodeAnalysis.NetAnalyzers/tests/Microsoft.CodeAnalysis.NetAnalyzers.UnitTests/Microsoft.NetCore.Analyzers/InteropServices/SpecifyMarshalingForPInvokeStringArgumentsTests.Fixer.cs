// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.PInvokeDiagnosticAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.InteropServices.CSharpSpecifyMarshalingForPInvokeStringArgumentsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.PInvokeDiagnosticAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.InteropServices.BasicSpecifyMarshalingForPInvokeStringArgumentsFixer>;

namespace Microsoft.NetCore.Analyzers.InteropServices.UnitTests
{
    public class SpecifyMarshalingForPInvokeStringArgumentsFixerTests
    {
        #region CA2101 Fixer tests 

        [Fact]
        public async Task CA2101FixMarshalAsCSharpTestAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System.Runtime.InteropServices;
using System.Text;

class C
{
    [DllImport(""user32.dll"")]
    private static extern void SomeMethod1([{|CA2101:MarshalAs(UnmanagedType.LPStr)|}] string s, [{|CA2101:MarshalAs(UnmanagedType.LPStr)|}] StringBuilder t);

    [DllImport(""user32.dll"")]
    private static extern void SomeMethod2([{|CA2101:MarshalAs((short)0)|}] string s);
}
", @"
using System.Runtime.InteropServices;
using System.Text;

class C
{
    [DllImport(""user32.dll"")]
    private static extern void SomeMethod1([MarshalAs(UnmanagedType.LPWStr)] string s, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder t);

    [DllImport(""user32.dll"")]
    private static extern void SomeMethod2([MarshalAs(UnmanagedType.LPWStr)] string s);
}
");
        }

        [Fact]
        public async Task CA2101FixMarshalAsBasicTestAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System.Runtime.InteropServices
Imports System.Text

Class C
    <DllImport(""user32.dll"")>
    Private Shared Sub SomeMethod1(<{|CA2101:MarshalAs(UnmanagedType.LPStr)|}> s As String, <{|CA2101:MarshalAs(UnmanagedType.LPStr)|}> t As StringBuilder)
    End Sub

    <DllImport(""user32.dll"")>
    Private Shared Sub SomeMethod2(<{|CA2101:MarshalAs(CShort(0))|}> s As String)
    End Sub

    Private Declare Sub SomeMethod3 Lib ""user32.dll"" (<{|CA2101:MarshalAs(UnmanagedType.LPStr)|}> s As String)
End Class
", @"
Imports System.Runtime.InteropServices
Imports System.Text

Class C
    <DllImport(""user32.dll"")>
    Private Shared Sub SomeMethod1(<MarshalAs(UnmanagedType.LPWStr)> s As String, <MarshalAs(UnmanagedType.LPWStr)> t As StringBuilder)
    End Sub

    <DllImport(""user32.dll"")>
    Private Shared Sub SomeMethod2(<MarshalAs(UnmanagedType.LPWStr)> s As String)
    End Sub

    Private Declare Sub SomeMethod3 Lib ""user32.dll"" (<MarshalAs(UnmanagedType.LPWStr)> s As String)
End Class
");
        }

        [Fact]
        public async Task CA2101FixCharSetCSharpTestAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System.Runtime.InteropServices;
using System.Text;

class C
{
    [{|CA2101:DllImport(""user32.dll"")|}]
    private static extern void SomeMethod1(string s);

    [{|CA2101:DllImport(""user32.dll"", CharSet = CharSet.Ansi)|}]
    private static extern void SomeMethod2(string s);
}
", @"
using System.Runtime.InteropServices;
using System.Text;

class C
{
    [DllImport(""user32.dll"", CharSet = CharSet.Unicode)]
    private static extern void SomeMethod1(string s);

    [DllImport(""user32.dll"", CharSet = CharSet.Unicode)]
    private static extern void SomeMethod2(string s);
}
");
        }

        [Fact]
        public async Task CA2101FixCharSetBasicTestAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System.Runtime.InteropServices
Imports System.Text

Class C
    <{|CA2101:DllImport(""user32.dll"")|}>
    Private Shared Sub SomeMethod1(s As String)
    End Sub

    <{|CA2101:DllImport(""user32.dll"", CharSet:=CharSet.Ansi)|}>
    Private Shared Sub SomeMethod2(s As String)
    End Sub

    <{|CA2101:DllImport(""user32.dll"", CHARSET:=CharSet.Ansi)|}>
    Private Shared Sub SomeMethod3(s As String)
    End Sub
End Class
", @"
Imports System.Runtime.InteropServices
Imports System.Text

Class C
    <DllImport(""user32.dll"", CharSet:=CharSet.Unicode)>
    Private Shared Sub SomeMethod1(s As String)
    End Sub

    <DllImport(""user32.dll"", CharSet:=CharSet.Unicode)>
    Private Shared Sub SomeMethod2(s As String)
    End Sub

    <DllImport(""user32.dll"", CharSet:=CharSet.Unicode)>
    Private Shared Sub SomeMethod3(s As String)
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA2101FixDeclareBasicTestAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System.Text

Class C
    Private Declare Sub {|CA2101:SomeMethod1|} Lib ""user32.dll"" (s As String)
    Private Declare Ansi Sub {|CA2101:SomeMethod2|} Lib ""user32.dll"" (s As StringBuilder)
    Private Declare Function {|CA2101:SomeMethod3|} Lib ""user32.dll"" () As String
End Class
", @"
Imports System.Text

Class C
    Private Declare Unicode Sub SomeMethod1 Lib ""user32.dll"" (s As String)
    Private Declare Unicode Sub SomeMethod2 Lib ""user32.dll"" (s As StringBuilder)
    Private Declare Unicode Function SomeMethod3 Lib ""user32.dll"" () As String
End Class
");
        }

        #endregion
    }
}
