// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AvoidEmptyInterfacesAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpAvoidEmptyInterfacesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AvoidEmptyInterfacesAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicAvoidEmptyInterfacesFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class AvoidEmptyInterfacesTests
    {
        [Fact]
        public async Task TestCSharpEmptyPublicInterface()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface I
{
}", CreateCSharpResult(2, 18));
        }

        [Fact]
        public async Task TestBasicEmptyPublicInterface()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface I
End Interface", CreateBasicResult(2, 18));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TestCSharpEmptyInternalInterface()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
interface I
{
}");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TestBasicEmptyInternalInterface()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Interface I
End Interface");
        }

        [Fact]
        public async Task TestCSharpNonEmptyInterface1()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface I
{
    void DoStuff();
}");
        }

        [Fact]
        public async Task TestBasicNonEmptyInterface1()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface I
    Function GetStuff() as Integer
End Interface");
        }

        [Fact]
        public async Task TestCSharpEmptyInterfaceWithNoInheritedMembers()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface I : IBase
{
}

public interface IBase { }", CreateCSharpResult(2, 18), CreateCSharpResult(6, 18));
        }

        [Fact]
        public async Task TestBasicEmptyInterfaceWithNoInheritedMembers()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface I
    Inherits IBase
End Interface

Public Interface IBase
End Interface", CreateBasicResult(2, 18), CreateBasicResult(6, 18));
        }

        [Fact]
        public async Task TestCSharpEmptyInterfaceWithInheritedMembers()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface I : IBase
{
}

public interface IBase
{
    void DoStuff();
}");
        }

        [Fact]
        public async Task TestBasicEmptyInterfaceWithInheritedMembers()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface I
    Inherits IBase
End Interface

Public Interface IBase
    Sub DoStuff()
End Interface");
        }

        [Theory]
        // General analyzer option
        [InlineData("public", "dotnet_code_quality.api_surface = public")]
        [InlineData("public", "dotnet_code_quality.api_surface = private, internal, public")]
        [InlineData("public", "dotnet_code_quality.api_surface = all")]
        [InlineData("protected", "dotnet_code_quality.api_surface = public")]
        [InlineData("protected", "dotnet_code_quality.api_surface = private, internal, public")]
        [InlineData("protected", "dotnet_code_quality.api_surface = all")]
        [InlineData("internal", "dotnet_code_quality.api_surface = internal")]
        [InlineData("internal", "dotnet_code_quality.api_surface = private, internal")]
        [InlineData("internal", "dotnet_code_quality.api_surface = all")]
        [InlineData("private", "dotnet_code_quality.api_surface = private")]
        [InlineData("private", "dotnet_code_quality.api_surface = private, public")]
        [InlineData("private", "dotnet_code_quality.api_surface = all")]
        // Specific analyzer option
        [InlineData("internal", "dotnet_code_quality.CA1040.api_surface = all")]
        [InlineData("internal", "dotnet_code_quality.Design.api_surface = all")]
        // General + Specific analyzer option
        [InlineData("internal", @"dotnet_code_quality.api_surface = private
                                  dotnet_code_quality.CA1040.api_surface = all")]
        // Case-insensitive analyzer option
        [InlineData("internal", "DOTNET_code_quality.CA1040.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [InlineData("internal", @"dotnet_code_quality.api_surface = all
                                  dotnet_code_quality.CA1040.api_surface_2 = private")]
        public async Task TestCSharpEmptyInterface_AnalyzerOptions_Diagnostic(string accessibility, string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
public class C
{{
    {accessibility} interface I {{ }}
}}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
                ExpectedDiagnostics =
                {
                    CreateCSharpResult(4, 16 + accessibility.Length),
                }
            }.RunAsync();
        }

        [Theory]
        // General analyzer option
        [InlineData("Public", "dotnet_code_quality.api_surface = Public")]
        [InlineData("Public", "dotnet_code_quality.api_surface = Private, Friend, Public")]
        [InlineData("Public", "dotnet_code_quality.api_surface = All")]
        [InlineData("Protected", "dotnet_code_quality.api_surface = Public")]
        [InlineData("Protected", "dotnet_code_quality.api_surface = Private, Friend, Public")]
        [InlineData("Protected", "dotnet_code_quality.api_surface = All")]
        [InlineData("Friend", "dotnet_code_quality.api_surface = Friend")]
        [InlineData("Friend", "dotnet_code_quality.api_surface = Private, Friend")]
        [InlineData("Friend", "dotnet_code_quality.api_surface = All")]
        [InlineData("Private", "dotnet_code_quality.api_surface = Private")]
        [InlineData("Private", "dotnet_code_quality.api_surface = Private, Public")]
        [InlineData("Private", "dotnet_code_quality.api_surface = All")]
        // Specific analyzer option
        [InlineData("Friend", "dotnet_code_quality.CA1040.api_surface = All")]
        [InlineData("Friend", "dotnet_code_quality.Design.api_surface = All")]
        // General + Specific analyzer option
        [InlineData("Friend", @"dotnet_code_quality.api_surface = Private
                                dotnet_code_quality.CA1040.api_surface = All")]
        // Case-insensitive analyzer option
        [InlineData("Friend", "DOTNET_code_quality.CA1040.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [InlineData("Friend", @"dotnet_code_quality.api_surface = All
                                dotnet_code_quality.CA1040.api_surface_2 = Private")]
        public async Task TestBasicEmptyInterface_AnalyzerOptions_Diagnostic(string accessibility, string editorConfigText)
        {
            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Public Class C
    {accessibility} Interface I
    End Interface
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
                ExpectedDiagnostics =
                {
                    CreateBasicResult(3, 16 + accessibility.Length),
                }
            }.RunAsync();
        }

        [Theory]
        [InlineData("public", "dotnet_code_quality.api_surface = private")]
        [InlineData("public", "dotnet_code_quality.CA1040.api_surface = internal, private")]
        [InlineData("public", "dotnet_code_quality.Design.api_surface = internal, private")]
        [InlineData("public", @"dotnet_code_quality.api_surface = all
                                  dotnet_code_quality.CA1040.api_surface = private")]
        public async Task TestCSharpEmptyInterface_AnalyzerOptions_NoDiagnostic(string accessibility, string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
public class C
{{
    {accessibility} interface I {{ }}
}}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            }.RunAsync();
        }

        [Theory]
        [InlineData("Public", "dotnet_code_quality.api_surface = Private")]
        [InlineData("Public", "dotnet_code_quality.CA1040.api_surface = Friend, Private")]
        [InlineData("Public", "dotnet_code_quality.Design.api_surface = Friend, Private")]
        [InlineData("Public", @"dotnet_code_quality.api_surface = All
                                dotnet_code_quality.CA1040.api_surface = Private")]
        public async Task TestBasicEmptyInterface_AnalyzerOptions_NoDiagnostic(string accessibility, string editorConfigText)
        {
            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Public Class C
    {accessibility} Interface I
    End Interface
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            }.RunAsync();
        }

        [Theory(Skip = "https://github.com/dotnet/roslyn-analyzers/issues/3494")]
        [CombinatorialData]
        public async Task TestConflictingAnalyzerOptionsForPartials(bool hasConflict)
        {
            var csTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"public partial interface I { }",
                        @"public partial interface I { }",
                    }
                },
                SolutionTransforms = { ApplyTransform }
            };

            if (!hasConflict)
            {
                csTest.ExpectedDiagnostics.Add(VerifyCS.Diagnostic().WithSpan(@"/folder1/Test0.cs", 1, 26, 1, 27).WithSpan(@"/folder2\Test1.cs", 1, 26, 1, 27));
            }

            await csTest.RunAsync();

            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Partial Interface I
End Interface",
                        @"
Public Partial Interface I
End Interface"
                    },
                },
                SolutionTransforms = { ApplyTransform }
            };

            if (!hasConflict)
            {
                vbTest.ExpectedDiagnostics.Add(VerifyVB.Diagnostic().WithSpan(@"/folder1/Test0.vb", 2, 26, 2, 27).WithSpan(@"/folder2/Test1.vb", 2, 26, 2, 27));
            }

            await vbTest.RunAsync();
            return;

            Solution ApplyTransform(Solution solution, ProjectId projectId)
            {
                var project = solution.GetProject(projectId)!;
                var projectFilePath = project.Language == LanguageNames.CSharp ? @"/Test.csproj" : @"/Test.vbproj";
                solution = solution.WithProjectFilePath(projectId, projectFilePath);

                var documentExtension = project.Language == LanguageNames.CSharp ? "cs" : "vb";
                var document1EditorConfig = $"[*.{documentExtension}]" + Environment.NewLine + "dotnet_code_quality.api_surface = public";
                var document2OptionValue = hasConflict ? "internal" : "public";
                var document2EditorConfig = $"[*.{documentExtension}]" + Environment.NewLine + $"dotnet_code_quality.api_surface = {document2OptionValue}";

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

        private static DiagnosticResult CreateCSharpResult(int line, int col)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, col);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult CreateBasicResult(int line, int col)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, col);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}