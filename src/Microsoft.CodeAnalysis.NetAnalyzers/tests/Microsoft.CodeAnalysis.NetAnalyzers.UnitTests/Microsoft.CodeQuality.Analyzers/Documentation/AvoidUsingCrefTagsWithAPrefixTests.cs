// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeQuality.CSharp.Analyzers.Documentation;
using Microsoft.CodeQuality.VisualBasic.Analyzers.Documentation;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.Documentation.CSharpAvoidUsingCrefTagsWithAPrefixAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.Documentation.CSharpAvoidUsingCrefTagsWithAPrefixFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.Documentation.BasicAvoidUsingCrefTagsWithAPrefixAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.Documentation.BasicAvoidUsingCrefTagsWithAPrefixFixer>;

namespace Microsoft.CodeQuality.Analyzers.Documentation.UnitTests
{
    public class AvoidUsingCrefTagsWithAPrefixTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new BasicAvoidUsingCrefTagsWithAPrefixAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new CSharpAvoidUsingCrefTagsWithAPrefixAnalyzer();
        }

        #region No Diagnostic Tests

        [Fact]
        public void NoDiagnosticCases()
        {
            VerifyCSharp(@"
/// <summary>
/// Type <see cref=""C"" /> contains method <see cref=""C.F"" />
/// This one is a dummy cref without kind prefix <see cref="":C.F"" />, <see cref=""T : C.F"" />
/// </summary>
class C
{
    public void F() { }
}
");

            VerifyBasic(@"
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
        public void DiagnosticCases()
        {
            VerifyCSharp(@"
/// <summary>
/// Type <see cref=""T:C""/> contains method <see cref=""M:C.F"" />
/// </summary>
class C
{
    public void F() { }
}
",
    // Test0.cs(3,21): warning RS0010: Avoid using cref tags with a prefix
    GetCSharpResultAt(3, 21),
    // Test0.cs(3,55): warning RS0010: Avoid using cref tags with a prefix
    GetCSharpResultAt(3, 55));

            VerifyBasic(@"
''' <summary>
''' Type <see cref=""T:C""/> contains method <see cref=""M:C.F"" />
''' </summary>
Class C
    Public Sub F()
    End Sub
End Class
",
    // Test0.vb(3,21): warning RS0010: Avoid using cref tags with a prefix
    GetBasicResultAt(3, 21),
    // Test0.vb(3,55): warning RS0010: Avoid using cref tags with a prefix
    GetBasicResultAt(3, 55));
        }

        #endregion

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
        {
            return GetCSharpResultAt(line, column, AvoidUsingCrefTagsWithAPrefixAnalyzer.RuleId, MicrosoftCodeQualityAnalyzersResources.AvoidUsingCrefTagsWithAPrefixMessage);
        }

        private static DiagnosticResult GetBasicResultAt(int line, int column)
        {
            return GetBasicResultAt(line, column, AvoidUsingCrefTagsWithAPrefixAnalyzer.RuleId, MicrosoftCodeQualityAnalyzersResources.AvoidUsingCrefTagsWithAPrefixMessage);
        }
    }
}