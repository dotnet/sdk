// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.Documentation.CSharpAvoidUsingCrefTagsWithAPrefixAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.Documentation.CSharpAvoidUsingCrefTagsWithAPrefixFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.Documentation.BasicAvoidUsingCrefTagsWithAPrefixAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.Documentation.BasicAvoidUsingCrefTagsWithAPrefixFixer>;

namespace Microsoft.CodeQuality.Analyzers.Documentation.UnitTests
{
    public class AvoidUsingCrefTagsWithAPrefixTests
    {
        #region No Diagnostic Tests

        [Fact]
        public async Task NoDiagnosticCasesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
/// <summary>
/// Type <see cref=""C"" /> contains method <see cref=""C.F"" />
/// This one is a dummy cref without kind prefix <see cref="":C.F"" />, <see cref=""T : C.F"" />
/// </summary>
class C
{
    public void F() { }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
''' <summary>
''' Type <see cref=""C""/> contains method <see cref=""C.F"" />
''' This one is a dummy cref without kind prefix <see cref="":C.F"" />, <see cref=""T : C.F"" />
''' </summary>
Class C
    Public Sub F()
    End Sub
End Class
");
        }

        #endregion

        #region Diagnostic Tests

        [Fact]
        public async Task DiagnosticCasesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
/// <summary>
/// Type <see cref=""T:C""/> contains method <see cref=""M:C.F"" />
/// </summary>
class C
{
    public void F() { }
}
",
    // Test0.cs(3,21): warning CA1200: Avoid using cref tags with a prefix
    GetCSharpResultAt(3, 21),
    // Test0.cs(3,55): warning CA1200: Avoid using cref tags with a prefix
    GetCSharpResultAt(3, 55));

            await VerifyVB.VerifyAnalyzerAsync(@"
''' <summary>
''' Type <see cref=""T:C""/> contains method <see cref=""M:C.F"" />
''' </summary>
Class C
    Public Sub F()
    End Sub
End Class
",
    // Test0.vb(3,21): warning CA1200: Avoid using cref tags with a prefix
    GetBasicResultAt(3, 21),
    // Test0.vb(3,55): warning CA1200: Avoid using cref tags with a prefix
    GetBasicResultAt(3, 55));
        }

        #endregion

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not use banned APIs

        private static DiagnosticResult GetBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not use banned APIs
    }
}