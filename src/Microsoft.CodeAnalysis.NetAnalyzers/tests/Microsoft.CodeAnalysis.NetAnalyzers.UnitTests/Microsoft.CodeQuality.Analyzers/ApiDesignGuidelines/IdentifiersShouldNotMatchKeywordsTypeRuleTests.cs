// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities;
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
    public class IdentifiersShouldNotMatchKeywordsTypeRuleTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new IdentifiersShouldNotMatchKeywordsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new IdentifiersShouldNotMatchKeywordsAnalyzer();
        }

        [Fact]
        public void CSharpDiagnosticForKeywordNamedPublicType()
        {
            VerifyCSharp(@"
public class @class {}
",
                GetCSharpResultAt(2, 14, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "class", "class"));
        }

        [Fact]
        public void BasicDiagnosticForKeywordNamedPublicType()
        {
            VerifyBasic(@"
Public Class [Class]
End Class
",
                GetBasicResultAt(2, 14, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "Class", "Class"));
        }

        [Fact]
        public void CSharpNoDiagnosticForCaseSensitiveKeywordNamedPublicTypeWithDifferentCasing()
        {
            VerifyCSharp(@"
public class iNtErNaL {}
");
        }

        [Fact]
        public void BasicNoDiagnosticForCaseSensitiveKeywordNamedPublicTypeWithDifferentCasing()
        {
            VerifyBasic(@"
Public Class iNtErNaL
End Class");
        }

        [Fact]
        public void CSharpDiagnosticForCaseInsensitiveKeywordNamedPublicType()
        {
            VerifyCSharp(@"
public struct aDdHaNdLeR {}
",
                GetCSharpResultAt(2, 15, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "aDdHaNdLeR", "AddHandler"));
        }

        [Fact]
        public void BasicDiagnosticForCaseInsensitiveKeywordNamedPublicType()
        {
            VerifyBasic(@"
Public Structure [aDdHaNdLeR]
End Structure",
                GetBasicResultAt(2, 18, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "aDdHaNdLeR", "AddHandler"));
        }

        [Fact]
        public void CSharpNoDiagnosticForKeywordNamedInternalype()
        {
            VerifyCSharp(@"
internal class @class {}
");
        }

        [Fact]
        public void BasicNoDiagnosticForKeywordNamedInternalType()
        {
            VerifyBasic(@"
Friend Class [Class]
End Class
");
        }

        [Fact]
        public void CSharpNoDiagnosticForNonKeywordNamedPublicType()
        {
            VerifyCSharp(@"
public class classic {}
");
        }

        [Fact]
        public void BasicNoDiagnosticForNonKeywordNamedPublicType()
        {
            VerifyBasic(@"
Public Class Classic
End Class
");
        }

        [Fact]
        public void CSharpDiagnosticForKeywordNamedPublicTypeInNamespace()
        {
            VerifyCSharp(@"
namespace N
{
    public enum @enum {}
}
",
                GetCSharpResultAt(4, 17, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "enum", "enum"));
        }

        [Fact]
        public void BasicDiagnosticForKeywordNamedPublicTypeInNamespace()
        {
            VerifyBasic(@"
Namespace N
    Public Enum [Enum]
        X
    End Enum
End Namespace
",
                GetBasicResultAt(3, 17, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "Enum", "Enum"));
        }

        [Fact]
        public void CSharpDiagnosticForKeywordNamedProtectedTypeNestedInPublicClass()
        {
            VerifyCSharp(@"
public class C
{
    protected class @protected {}
}
",
                GetCSharpResultAt(4, 21, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "C.protected", "protected"));
        }

        [Fact]
        public void BasicDiagnosticForKeywordNamedProtectedTypeNestedInPublicClass()
        {
            VerifyBasic(@"
Public Class C
    Protected Class [Protected]
    End Class
End Class
",
                GetBasicResultAt(3, 21, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "C.Protected", "Protected"));
        }
    }
}
