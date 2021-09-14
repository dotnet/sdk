// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.DoNotUseOutAttributeStringPInvokeParametersAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.DoNotUseOutAttributeStringPInvokeParametersAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.InteropServices.UnitTests
{
    public class DoNotUseOutAttributeStringPInvokeParametersAnalyzerTests
    {
        [Fact]
        public async Task StringByReference_NoDiagnostics_CS()
        {
            string source = @"
using System.Runtime.InteropServices;

public class C
{
    [DllImport(""user32.dll"", CharSet=CharSet.Unicode)]
    private static extern void Method1(out string s); // OK

    [DllImport(""user32.dll"", CharSet=CharSet.Unicode)]
    private static extern void Method2([In] [Out] ref string s); // OK

    [DllImport(""user32.dll"", CharSet=CharSet.Unicode)]
    private static extern void Method3([Out] out string s); // OK
}
";
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NotPInvoke_NoDiagnostics_CS()
        {
            string source = @"
using System.Runtime.InteropServices;

public class C
{
    private static extern void Method1([Out] string s);
}
";
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task OutAttributeStringByValue_Diagnostics_CS()
        {
            string source = @"
using System.Runtime.InteropServices;

public class C
{
    [DllImport(""user32.dll"", CharSet=CharSet.Unicode)]
    private static extern void Method1([Out] string {|#0:s|}); // Should not have [Out] string

    [DllImport(""user32.dll"", CharSet=CharSet.Unicode)]
    private static extern void Method2(string s1, [Out] string {|#1:s2|}, [Out] string {|#2:s3|}); // Should not have [Out] string
}
";
            await VerifyCS.VerifyAnalyzerAsync(
                source,
                CSharpResult(0, "s"),
                CSharpResult(1, "s2"),
                CSharpResult(2, "s3"));
        }

        [Fact]
        public async Task StringByReference_NoDiagnostics_VB()
        {
            string source = @"
Imports System.Runtime.InteropServices

Class C
    <DllImport(""user32.dll"", CharSet:=CharSet.Unicode)>
    Private Shared Sub Method1(<Out()> ByRef s As String) ' OK
    End Sub
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NotPInvoke_NoDiagnostics_VB()
        {
            string source = @"
Imports System.Runtime.InteropServices

Class C
    Private Shared Sub Method1(<Out()> s As String)
    End Sub
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task OutAttributeStringByValue_Diagnostics_VB()
        {
            string source = @"
Imports System.Runtime.InteropServices

Class C
    <DllImport(""user32.dll"", CharSet:=CharSet.Unicode)>
    Private Shared Sub Method1(<Out()> {|#0:s|} As String) ' Should not have <Out> string
    End Sub

    <DllImport(""user32.dll"", CharSet:=CharSet.Unicode)>
    Private Shared Sub Method2(s1 As String, <Out()> {|#1:s2|} As String, <Out()> {|#2:s3|} As String) ' Should not have <Out> string
    End Sub
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(
                source,
                BasicResult(0, "s"),
                BasicResult(1, "s2"),
                BasicResult(2, "s3"));
        }

        private DiagnosticResult CSharpResult(int markupKey, params string[] arguments)
           => VerifyCS.Diagnostic(DoNotUseOutAttributeStringPInvokeParametersAnalyzer.Rule)
               .WithLocation(markupKey)
               .WithArguments(arguments);

        private DiagnosticResult BasicResult(int markupKey, params string[] arguments)
            => VerifyVB.Diagnostic(DoNotUseOutAttributeStringPInvokeParametersAnalyzer.Rule)
                .WithLocation(markupKey)
                .WithArguments(arguments);
    }
}
