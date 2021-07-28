// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
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

        [Theory]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = Method")]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = Method, Property")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = Method")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = Method, Property")]
        public async Task UserOptionDoesNotIncludeNamedType_NoDiagnostic(string editorConfigText)
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
            }.RunAsync();

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
            }.RunAsync();
        }

        [Theory]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = NamedType")]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = NamedType, Property")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Property")]
        public async Task UserOptionIncludesNamedType_Diagnostic(string editorConfigText)
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
            }.RunAsync();

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
            }.RunAsync();
        }

        [Theory(Skip = "https://github.com/dotnet/roslyn-analyzers/issues/3494")]
        // Identical
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = NamedType", "dotnet_code_quality.analyzed_symbol_kinds = NamedType", true)]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Property", "dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Property", true)]
        // Different, intersection has 'NamedType'
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = NamedType, Property", "dotnet_code_quality.analyzed_symbol_kinds = NamedType", true)]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Property", "dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Method", true)]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = NamedType", "", true)] // Default has 'NamedType'
        // Different, intersection does not have 'NamedType'
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = NamedType, Property", "dotnet_code_quality.analyzed_symbol_kinds = Property", false)]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Method", "dotnet_code_quality.analyzed_symbol_kinds = Property", false)]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = Method", "", false)] // Default has 'NamedType'
        public async Task TestConflictingAnalyzerOptionsForPartials(string editorConfigText1, string editorConfigText2, bool expectDiagnostic)
        {
            var csTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"public partial class @class {}",
                        @"public partial class @class {}",
                    }
                },
                SolutionTransforms = { ApplyTransform }
            };

            if (expectDiagnostic)
            {
                csTest.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule).WithSpan(@"/folder1/Test0.cs", 1, 22, 1, 28).WithSpan(@"/folder2/Test1.cs", 1, 22, 1, 28).WithArguments("class", "class"));
            }

            await csTest.RunAsync();

            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Partial Class [Class]
End Class",
                        @"
Public Partial Class [Class]
End Class"
                    },
                },
                SolutionTransforms = { ApplyTransform }
            };

            if (expectDiagnostic)
            {
                vbTest.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(IdentifiersShouldNotMatchKeywordsAnalyzer.TypeRule).WithSpan(@"/folder1\Test0.vb", 2, 22, 2, 29).WithSpan(@"/folder2\Test1.vb", 2, 22, 2, 29).WithArguments("Class", "Class"));
            }

            await vbTest.RunAsync();
            return;

            Solution ApplyTransform(Solution solution, ProjectId projectId)
            {
                var project = solution.GetProject(projectId)!;
                var projectFilePath = project.Language == LanguageNames.CSharp ? @"/Test.csproj" : @"/Test.vbproj";
                solution = solution.WithProjectFilePath(projectId, projectFilePath);

                var documentExtension = project.Language == LanguageNames.CSharp ? "cs" : "vb";
                var document1EditorConfig = $"[*.{documentExtension}]" + Environment.NewLine + editorConfigText1;
                var document2EditorConfig = $"[*.{documentExtension}]" + Environment.NewLine + editorConfigText2;

                var document1Folder = $@"/folder1";
                solution = solution.WithDocumentFilePath(project.DocumentIds[0], $@"{document1Folder}\Test0.{documentExtension}");
                solution = solution.GetProject(projectId)!
                    .AddAnalyzerConfigDocument(
                        ".editorconfig",
                        SourceText.From(document1EditorConfig),
                        filePath: $@"{document1Folder}\.editorconfig")
                    .Project.Solution;

                var document2Folder = $@"/folder2";
                solution = solution.WithDocumentFilePath(project.DocumentIds[1], $@"{document2Folder}\Test1.{documentExtension}");
                return solution.GetProject(projectId)!
                    .AddAnalyzerConfigDocument(
                        ".editorconfig",
                        SourceText.From(document2EditorConfig),
                        filePath: $@"{document2Folder}\.editorconfig")
                    .Project.Solution;
            }
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, string arg1, string arg2)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arg1, arg2);

        private static DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule, string arg1, string arg2)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arg1, arg2);
    }
}
