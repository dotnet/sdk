// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
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
    [TestClass]
    public class IdentifiersShouldNotMatchKeywordsNamespaceRuleTests
    {
        [TestMethod]
        public async Task CSharpDiagnosticForKeywordNamedNamespaceContainingPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace @namespace
{
    public class C {}
}
",
                GetCSharpResultAt(2, 11, IdentifiersShouldNotMatchKeywordsAnalyzer.NamespaceRule, "namespace", "namespace"));
        }

        [TestMethod]
        public async Task BasicDiagnosticForKeywordNamedNamespaceContainingPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Namespace [Namespace]
    Public Class C
    End Class
End Namespace
",
            GetBasicResultAt(2, 11, IdentifiersShouldNotMatchKeywordsAnalyzer.NamespaceRule, "Namespace", "Namespace"));
        }

        [TestMethod]
        public async Task CSharpNoDiagnosticForNonKeywordNamedNamespaceContainingPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace namespace2
{
    public class C {}
}
");
        }

        [TestMethod]
        public async Task BasicNoDiagnosticForNonKeywordNamedNamespaceContainingPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Namespace Namespace2
    Public Class C
    End Class
End Namespace
");
        }

        [TestMethod]
        public async Task CSharpNoDiagnosticForKeywordNamedNamespaceContainingInternalClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace @namespace
{
    internal class C {}
}
");
        }

        [TestMethod]
        public async Task BasicNoDiagnosticForKeywordNamedNamespaceContainingInternalClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Namespace [Namespace]
    Friend Class C
    End Class
End Namespace
");
        }

        [TestMethod]
        public async Task CSharpDiagnosticForKeywordNamedMultiComponentNamespaceContainingPublicClassAsync()
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

        [TestMethod]
        public async Task BasicDiagnosticForKeywordNamedMultiComponentNamespaceContainingPublicClassAsync()
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

        [TestMethod]
        public async Task CSharpNoDiagnosticForPublicClassInGlobalNamespaceAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C {}
");
        }

        [TestMethod]
        public async Task BasicNoDiagnosticForPublicClassInGlobalNamespaceAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
End Class
");
        }

        [TestMethod]
        public async Task CSharpNoDiagnosticForRepeatedOccurrencesOfSameKeywordNamedNamespaceAsync()
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

        [TestMethod]
        public async Task BasicNoDiagnosticForRepeatedOccurrencesOfSameKeywordNamedNamespaceAsync()
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

        [TestMethod]
        [DataRow("dotnet_code_quality.analyzed_symbol_kinds = NamedType")]
        [DataRow("dotnet_code_quality.analyzed_symbol_kinds = Method, Property")]
        [DataRow("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType")]
        [DataRow("dotnet_code_quality.CA1716.analyzed_symbol_kinds = Method, Property")]
        public async Task UserOptionDoesNotIncludeNamespace_NoDiagnosticAsync(string editorConfigText)
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
            }.RunAsync(CancellationToken.None);

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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("dotnet_code_quality.analyzed_symbol_kinds = Namespace")]
        [DataRow("dotnet_code_quality.analyzed_symbol_kinds = Namespace, Property")]
        [DataRow("dotnet_code_quality.CA1716.analyzed_symbol_kinds = Namespace")]
        [DataRow("dotnet_code_quality.CA1716.analyzed_symbol_kinds = Namespace, Property")]
        public async Task UserOptionIncludesNamespace_DiagnosticAsync(string editorConfigText)
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
            }.RunAsync(CancellationToken.None);

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
            }.RunAsync(CancellationToken.None);
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arguments);
    }
}
