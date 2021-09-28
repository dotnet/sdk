// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.AvoidStringBuilderPInvokeParametersAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.AvoidStringBuilderPInvokeParametersAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.InteropServices.UnitTests
{
    public class AvoidStringBuilderPInvokeParametersTests
    {
        [Fact]
        public async Task NotPInvoke_NoDiagnostics_CSAsync()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Text;

public class C
{
    private static extern void Method(StringBuilder sb);
}
";
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task StringBuilderParameter_Diagnostics_CSAsync()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Text;

public class C
{
    [DllImport(""native.dll"")]
    private static extern void Method1(StringBuilder {|#0:sb|});

    [DllImport(""native.dll"")]
    private static extern void Method2(StringBuilder {|#1:sb1|}, StringBuilder {|#2:sb2|});
}
";
            await VerifyCS.VerifyAnalyzerAsync(
                source,
                CSharpResult(0, "sb"),
                CSharpResult(1, "sb1"),
                CSharpResult(2, "sb2"));
        }

        [Fact]
        public async Task NotPInvoke_NoDiagnostics_VBAsync()
        {
            string source = @"
Imports System.Runtime.InteropServices
Imports System.Text

Class C
    Private Shared Sub Method1(sb As StringBuilder)
    End Sub
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task StringBuilderParameter_Diagnostics_VBAsync()
        {
            string source = @"
Imports System.Runtime.InteropServices
Imports System.Text

Class C
    <DllImport(""native.dll"")>
    Private Shared Sub Method1({|#0:sb|} As StringBuilder)
    End Sub

    <DllImport(""native.dll"")>
    Private Shared Sub Method2({|#1:sb1|} As StringBuilder, {|#2:sb2|} As StringBuilder)
    End Sub
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(
                source,
                BasicResult(0, "sb"),
                BasicResult(1, "sb1"),
                BasicResult(2, "sb2"));
        }

        private DiagnosticResult CSharpResult(int markupKey, params string[] arguments)
           => VerifyCS.Diagnostic(AvoidStringBuilderPInvokeParametersAnalyzer.Rule)
                .WithLocation(markupKey)
                .WithArguments(arguments);

        private DiagnosticResult BasicResult(int markupKey, params string[] arguments)
            => VerifyVB.Diagnostic(AvoidStringBuilderPInvokeParametersAnalyzer.Rule)
                .WithLocation(markupKey)
                .WithArguments(arguments);
    }
}
