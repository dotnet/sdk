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
    /// pertain to the NamespaceRule, which applies to the names of type namespaces.
    /// </summary>
    /// <remarks>
    /// FxCop does not report a violation unless the namespace contains a publicly visible
    /// class, and we follow that implementation.
    /// </remarks>
    public class IdentifiersShouldNotMatchKeywordsNamespaceRuleTests
    {
        [Fact]
        public async Task CSharpDiagnosticForKeywordNamedNamespaceContainingPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace @namespace
{
    public class C {}
}
",
                GetCSharpResultAt(2, 11, IdentifiersShouldNotMatchKeywordsAnalyzer.NamespaceRule, "namespace", "namespace"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedNamespaceContainingPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Namespace [Namespace]
    Public Class C
    End Class
End Namespace
",
            GetBasicResultAt(2, 11, IdentifiersShouldNotMatchKeywordsAnalyzer.NamespaceRule, "Namespace", "Namespace"));
        }

        [Fact]
        public async Task CSharpNoDiagnosticForNonKeywordNamedNamespaceContainingPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace namespace2
{
    public class C {}
}
");
        }

        [Fact]
        public async Task BasicNoDiagnosticForNonKeywordNamedNamespaceContainingPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Namespace Namespace2
    Public Class C
    End Class
End Namespace
");
        }

        [Fact]
        public async Task CSharpNoDiagnosticForKeywordNamedNamespaceContainingInternalClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace @namespace
{
    internal class C {}
}
");
        }

        [Fact]
        public async Task BasicNoDiagnosticForKeywordNamedNamespaceContainingInternalClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Namespace [Namespace]
    Friend Class C
    End Class
End Namespace
");
        }

        [Fact]
        public async Task CSharpDiagnosticForKeywordNamedMultiComponentNamespaceContainingPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace N1.@namespace.N2.@for.N3
{
    public class C {}
}
",
                GetCSharpResultAt(2, 33, IdentifiersShouldNotMatchKeywordsAnalyzer.NamespaceRule, "N1.namespace.N2.for.N3", "namespace"),
                GetCSharpResultAt(2, 33, IdentifiersShouldNotMatchKeywordsAnalyzer.NamespaceRule, "N1.namespace.N2.for.N3", "for"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedMultiComponentNamespaceContainingPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Namespace N1.[Namespace].N2.[For].N3
    Public Class C
    End Class
End Namespace
",
                GetBasicResultAt(2, 35, IdentifiersShouldNotMatchKeywordsAnalyzer.NamespaceRule, "N1.Namespace.N2.For.N3", "Namespace"),
                GetBasicResultAt(2, 35, IdentifiersShouldNotMatchKeywordsAnalyzer.NamespaceRule, "N1.Namespace.N2.For.N3", "For"));
        }

        [Fact]
        public async Task CSharpNoDiagnosticForPublicClassInGlobalNamespace()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C {}
");
        }

        [Fact]
        public async Task BasicNoDiagnosticForPublicClassInGlobalNamespace()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
End Class
");
        }

        [Fact]
        public async Task CSharpNoDiagnosticForRepeatedOccurrencesOfSameKeywordNamedNamespace()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace @namespace
{
    public class C {}
}

namespace @namespace
{
    public class D {}
}",
                // Diagnostic for only one of the two occurrences.
                GetCSharpResultAt(2, 11, IdentifiersShouldNotMatchKeywordsAnalyzer.NamespaceRule, "namespace", "namespace"));
        }

        [Fact]
        public async Task BasicNoDiagnosticForRepeatedOccurrencesOfSameKeywordNamedNamespace()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Namespace [Namespace]
    Public Class C
    End Class
End Namespace

Namespace [Namespace]
    Public Class D
    End Class
End Namespace
",
                GetBasicResultAt(2, 11, IdentifiersShouldNotMatchKeywordsAnalyzer.NamespaceRule, "Namespace", "Namespace"));
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
            => new DiagnosticResult(rule)
                .WithLocation(line, column)
                .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
            => new DiagnosticResult(rule)
                .WithLocation(line, column)
                .WithArguments(arguments);
    }
}
