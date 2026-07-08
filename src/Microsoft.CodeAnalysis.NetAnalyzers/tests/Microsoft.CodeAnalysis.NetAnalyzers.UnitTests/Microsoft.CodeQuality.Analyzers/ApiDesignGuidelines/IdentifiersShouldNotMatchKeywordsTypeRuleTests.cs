// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
    /// pertain to the TypeRule, which applies to the names of types.
    /// </summary>
    [TestClass]
    public class IdentifiersShouldNotMatchKeywordsTypeRuleTests
    {
        [TestMethod]
        public async Task CSharpDiagnosticForKeywordNamedPublicTypeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class @class {}
",
                GetCSharpResultAt(2, 14, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "class", "class"));
        }

        [TestMethod]
        public async Task BasicDiagnosticForKeywordNamedPublicTypeAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class [Class]
End Class
",
                GetBasicResultAt(2, 14, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "Class", "Class"));
        }

        [TestMethod]
        public async Task CSharpNoDiagnosticForCaseSensitiveKeywordNamedPublicTypeWithDifferentCasingAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class iNtErNaL {}
");
        }

        [TestMethod]
        public async Task BasicNoDiagnosticForCaseSensitiveKeywordNamedPublicTypeWithDifferentCasingAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class iNtErNaL
End Class");
        }

        [TestMethod]
        public async Task CSharpDiagnosticForCaseInsensitiveKeywordNamedPublicTypeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct aDdHaNdLeR {}
",
                GetCSharpResultAt(2, 15, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "aDdHaNdLeR", "AddHandler"));
        }

        [TestMethod]
        public async Task BasicDiagnosticForCaseInsensitiveKeywordNamedPublicTypeAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Structure [aDdHaNdLeR]
End Structure",
                GetBasicResultAt(2, 18, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "aDdHaNdLeR", "AddHandler"));
        }

        [TestMethod]
        public async Task CSharpNoDiagnosticForKeywordNamedInternalypeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal class @class {}
");
        }

        [TestMethod]
        public async Task BasicNoDiagnosticForKeywordNamedInternalTypeAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Class [Class]
End Class
");
        }

        [TestMethod]
        public async Task CSharpNoDiagnosticForNonKeywordNamedPublicTypeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class classic {}
");
        }

        [TestMethod]
        public async Task BasicNoDiagnosticForNonKeywordNamedPublicTypeAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Classic
End Class
");
        }

        [TestMethod]
        public async Task CSharpDiagnosticForKeywordNamedPublicTypeInNamespaceAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace N
{
    public enum @enum {}
}
",
                GetCSharpResultAt(4, 17, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "enum", "enum"));
        }

        [TestMethod]
        public async Task BasicDiagnosticForKeywordNamedPublicTypeInNamespaceAsync()
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

        [TestMethod]
        public async Task CSharpDiagnosticForKeywordNamedProtectedTypeNestedInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    protected class @protected {}
}
",
                GetCSharpResultAt(4, 21, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "C.protected", "protected"));
        }

        [TestMethod]
        public async Task BasicDiagnosticForKeywordNamedProtectedTypeNestedInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Protected Class [Protected]
    End Class
End Class
",
                GetBasicResultAt(3, 21, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "C.Protected", "Protected"));
        }

        [TestMethod]
        [DataRow("dotnet_code_quality.analyzed_symbol_kinds = Method")]
        [DataRow("dotnet_code_quality.analyzed_symbol_kinds = Method, Property")]
        [DataRow("dotnet_code_quality.CA1716.analyzed_symbol_kinds = Method")]
        [DataRow("dotnet_code_quality.CA1716.analyzed_symbol_kinds = Method, Property")]
        public async Task UserOptionDoesNotIncludeNamedType_NoDiagnosticAsync(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"public class @class {}",
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
Public Class [Class]
End Class",
            },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("dotnet_code_quality.analyzed_symbol_kinds = NamedType")]
        [DataRow("dotnet_code_quality.analyzed_symbol_kinds = NamedType, Property")]
        [DataRow("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType")]
        [DataRow("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Property")]
        public async Task UserOptionIncludesNamedType_DiagnosticAsync(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"public class @class {}",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                    ExpectedDiagnostics = { GetCSharpResultAt(1, 14, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "class", "class"), },
                },
            }.RunAsync(CancellationToken.None);

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Class [Class]
End Class",
            },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                    ExpectedDiagnostics = { GetBasicResultAt(2, 14, IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule, "Class", "Class"), },
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        // Identical
        [DataRow("dotnet_code_quality.analyzed_symbol_kinds = NamedType", "dotnet_code_quality.analyzed_symbol_kinds = NamedType", true)]
        [DataRow("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Property", "dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Property", true)]
        // Different, intersection has 'NamedType'
        [DataRow("dotnet_code_quality.analyzed_symbol_kinds = NamedType, Property", "dotnet_code_quality.analyzed_symbol_kinds = NamedType", true)]
        [DataRow("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Property", "dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Method", true)]
        [DataRow("dotnet_code_quality.analyzed_symbol_kinds = NamedType", "", true)] // Default has 'NamedType'
        // Different, intersection does not have 'NamedType'
        // MSTest has no per-row skip; row disabled (was xUnit InlineData Skip): [DataRow("dotnet_code_quality.analyzed_symbol_kinds = NamedType, Property", "dotnet_code_quality.analyzed_symbol_kinds = Property", false, Skip = "https://github.com/dotnet/roslyn-analyzers/issues/3494")]
        // MSTest has no per-row skip; row disabled (was xUnit InlineData Skip): [DataRow("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Method", "dotnet_code_quality.analyzed_symbol_kinds = Property", false, Skip = "https://github.com/dotnet/roslyn-analyzers/issues/3494")]
        // MSTest has no per-row skip; row disabled (was xUnit InlineData Skip): [DataRow("dotnet_code_quality.analyzed_symbol_kinds = Method", "", false, Skip = "https://github.com/dotnet/roslyn-analyzers/issues/3494")] // Default has 'NamedType'
        public async Task TestConflictingAnalyzerOptionsForPartialsAsync(string editorConfigText1, string editorConfigText2, bool expectDiagnostic)
        {
            var csTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        ("/folder1/Test0.cs", @"public partial class @class {}"),
                        ("/folder2/Test1.cs", @"public partial class @class {}"),
                    },
                    AnalyzerConfigFiles =
                    {
                        ("/folder1/.editorconfig", $"[*.cs]" + Environment.NewLine + editorConfigText1),
                        ("/folder2/.editorconfig", $"[*.cs]" + Environment.NewLine + editorConfigText2),
                    },
                },
            };

            if (expectDiagnostic)
            {
                csTest.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule).WithSpan(@"/folder1/Test0.cs", 1, 22, 1, 28).WithSpan(@"/folder2/Test1.cs", 1, 22, 1, 28).WithArguments("class", "class"));
            }

            await csTest.RunAsync(CancellationToken.None);

            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        ("/folder1/Test0.vb", @"
Public Partial Class [Class]
End Class"),
                        ("/folder2/Test1.vb", @"
Public Partial Class [Class]
End Class"),
                    },
                    AnalyzerConfigFiles =
                    {
                        ("/folder1/.editorconfig", $"[*.cs]" + Environment.NewLine + editorConfigText1),
                        ("/folder2/.editorconfig", $"[*.cs]" + Environment.NewLine + editorConfigText2),
                    },
                },
            };

            if (expectDiagnostic)
            {
                vbTest.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule).WithSpan(@"/folder1/Test0.vb", 2, 22, 2, 29).WithSpan(@"/folder2/Test1.vb", 2, 22, 2, 29).WithArguments("Class", "Class"));
            }

            await vbTest.RunAsync(CancellationToken.None);
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, string arg1, string arg2)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arg1, arg2);

        private static DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule, string arg1, string arg2)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arg1, arg2);
    }
}
