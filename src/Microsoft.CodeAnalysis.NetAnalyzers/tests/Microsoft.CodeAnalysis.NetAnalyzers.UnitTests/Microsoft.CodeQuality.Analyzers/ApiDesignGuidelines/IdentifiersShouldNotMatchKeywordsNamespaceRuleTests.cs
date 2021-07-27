// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
                VerifyCS.Diagnostic(IdentifiersShouldNotMatchKeywordsAnalyzer.NamespaceRule)
                    .WithSpan(2, 11, 2, 21)
                    .WithSpan(7, 11, 7, 21)
                    .WithArguments("namespace", "namespace"));
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
                VerifyVB.Diagnostic(IdentifiersShouldNotMatchKeywordsAnalyzer.NamespaceRule)
                    .WithSpan(2, 11, 2, 22)
                    .WithSpan(7, 11, 7, 22)
                    .WithArguments("Namespace", "Namespace"));
        }

        [Theory]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = NamedType")]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = Method, Property")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = Method, Property")]
        public async Task UserOptionDoesNotIncludeNamespace_NoDiagnostic(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
namespace @namespace
{
    public class C {}
}
",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Namespace [Namespace]
    Public Class C
    End Class
End Namespace",
            },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                },
            }.RunAsync();
        }

        [Theory]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = Namespace")]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = Namespace, Property")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = Namespace")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = Namespace, Property")]
        public async Task UserOptionIncludesNamespace_Diagnostic(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
namespace @namespace
{
    public class C {}
}
",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                    ExpectedDiagnostics = { GetCSharpResultAt(2, 11, IdentifiersShouldNotMatchKeywordsAnalyzer.NamespaceRule, "namespace", "namespace"), },
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Namespace [Namespace]
    Public Class C
    End Class
End Namespace",
            },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                    ExpectedDiagnostics = { GetBasicResultAt(2, 11, IdentifiersShouldNotMatchKeywordsAnalyzer.NamespaceRule, "Namespace", "Namespace"), },
                },
            }.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
