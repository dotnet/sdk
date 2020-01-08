// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.IdentifiersShouldNotMatchKeywordsAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpIdentifiersShouldNotMatchKeywordsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.IdentifiersShouldNotMatchKeywordsAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicIdentifiersShouldNotMatchKeywordsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    /// <summary>
    /// Contains those unit tests for the IdentifiersShouldNotMatchKeywords analyzer that
    /// pertain to the TypeRule, which applies to the names of types.
    /// </summary>
    public class IdentifiersShouldNotMatchKeywordsTypeRuleTests
    {
        [Fact]
        public async Task CSharpDiagnosticForKeywordNamedPublicType()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class @class {}
",
                GetCSharpResultAt(2, 14, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "class", "class"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedPublicType()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class [Class]
End Class
",
                GetBasicResultAt(2, 14, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "Class", "Class"));
        }

        [Fact]
        public async Task CSharpNoDiagnosticForCaseSensitiveKeywordNamedPublicTypeWithDifferentCasing()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class iNtErNaL {}
");
        }

        [Fact]
        public async Task BasicNoDiagnosticForCaseSensitiveKeywordNamedPublicTypeWithDifferentCasing()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class iNtErNaL
End Class");
        }

        [Fact]
        public async Task CSharpDiagnosticForCaseInsensitiveKeywordNamedPublicType()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct aDdHaNdLeR {}
",
                GetCSharpResultAt(2, 15, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "aDdHaNdLeR", "AddHandler"));
        }

        [Fact]
        public async Task BasicDiagnosticForCaseInsensitiveKeywordNamedPublicType()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Structure [aDdHaNdLeR]
End Structure",
                GetBasicResultAt(2, 18, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "aDdHaNdLeR", "AddHandler"));
        }

        [Fact]
        public async Task CSharpNoDiagnosticForKeywordNamedInternalype()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal class @class {}
");
        }

        [Fact]
        public async Task BasicNoDiagnosticForKeywordNamedInternalType()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Class [Class]
End Class
");
        }

        [Fact]
        public async Task CSharpNoDiagnosticForNonKeywordNamedPublicType()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class classic {}
");
        }

        [Fact]
        public async Task BasicNoDiagnosticForNonKeywordNamedPublicType()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Classic
End Class
");
        }

        [Fact]
        public async Task CSharpDiagnosticForKeywordNamedPublicTypeInNamespace()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace N
{
    public enum @enum {}
}
",
                GetCSharpResultAt(4, 17, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "enum", "enum"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedPublicTypeInNamespace()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Namespace N
    Public Enum [Enum]
        X
    End Enum
End Namespace
",
                GetBasicResultAt(3, 17, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "Enum", "Enum"));
        }

        [Fact]
        public async Task CSharpDiagnosticForKeywordNamedProtectedTypeNestedInPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    protected class @protected {}
}
",
                GetCSharpResultAt(4, 21, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "C.protected", "protected"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedProtectedTypeNestedInPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Protected Class [Protected]
    End Class
End Class
",
                GetBasicResultAt(3, 21, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "C.Protected", "Protected"));
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, string arg1, string arg2)
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
                .WithArguments(arg1, arg2);

        private static DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule, string arg1, string arg2)
            => VerifyVB.Diagnostic(rule)
                .WithLocation(line, column)
                .WithArguments(arg1, arg2);
    }
}
