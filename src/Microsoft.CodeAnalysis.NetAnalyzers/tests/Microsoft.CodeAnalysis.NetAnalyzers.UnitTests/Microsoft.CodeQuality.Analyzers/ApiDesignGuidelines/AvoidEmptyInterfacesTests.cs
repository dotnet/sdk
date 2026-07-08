// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AvoidEmptyInterfacesAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpAvoidEmptyInterfacesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AvoidEmptyInterfacesAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicAvoidEmptyInterfacesFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    [TestClass]
    public class AvoidEmptyInterfacesTests
    {
        [TestMethod]
        public async Task TestCSharpEmptyPublicInterfaceAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface I
{
}", CreateCSharpResult(2, 18));
        }

        [TestMethod]
        public async Task TestBasicEmptyPublicInterfaceAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface I
End Interface", CreateBasicResult(2, 18));
        }

        [TestMethod, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TestCSharpEmptyInternalInterfaceAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
interface I
{
}");
        }

        [TestMethod, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TestBasicEmptyInternalInterfaceAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Interface I
End Interface");
        }

        [TestMethod]
        public async Task TestCSharpNonEmptyInterface1Async()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface I
{
    void DoStuff();
}");
        }

        [TestMethod]
        public async Task TestBasicNonEmptyInterface1Async()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface I
    Function GetStuff() as Integer
End Interface");
        }

        [TestMethod]
        public async Task TestCSharpEmptyInterfaceWithNoInheritedMembersAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface I : IBase
{
}

public interface IBase { }", CreateCSharpResult(2, 18), CreateCSharpResult(6, 18));
        }

        [TestMethod]
        public async Task TestBasicEmptyInterfaceWithNoInheritedMembersAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface I
    Inherits IBase
End Interface

Public Interface IBase
End Interface", CreateBasicResult(2, 18), CreateBasicResult(6, 18));
        }

        [TestMethod]
        public async Task TestCSharpEmptyInterfaceWithInheritedMembersAsync()
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

        [TestMethod]
        public async Task TestBasicEmptyInterfaceWithInheritedMembersAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface I
    Inherits IBase
End Interface

Public Interface IBase
    Sub DoStuff()
End Interface");
        }

        [TestMethod]
        // General analyzer option
        [DataRow("public", "dotnet_code_quality.api_surface = public")]
        [DataRow("public", "dotnet_code_quality.api_surface = private, internal, public")]
        [DataRow("public", "dotnet_code_quality.api_surface = all")]
        [DataRow("protected", "dotnet_code_quality.api_surface = public")]
        [DataRow("protected", "dotnet_code_quality.api_surface = private, internal, public")]
        [DataRow("protected", "dotnet_code_quality.api_surface = all")]
        [DataRow("internal", "dotnet_code_quality.api_surface = internal")]
        [DataRow("internal", "dotnet_code_quality.api_surface = private, internal")]
        [DataRow("internal", "dotnet_code_quality.api_surface = all")]
        [DataRow("private", "dotnet_code_quality.api_surface = private")]
        [DataRow("private", "dotnet_code_quality.api_surface = private, public")]
        [DataRow("private", "dotnet_code_quality.api_surface = all")]
        // Specific analyzer option
        [DataRow("internal", "dotnet_code_quality.CA1040.api_surface = all")]
        [DataRow("internal", "dotnet_code_quality.Design.api_surface = all")]
        // General + Specific analyzer option
        [DataRow("internal", @"dotnet_code_quality.api_surface = private
                                  dotnet_code_quality.CA1040.api_surface = all")]
        // Case-insensitive analyzer option
        [DataRow("internal", "DOTNET_code_quality.CA1040.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [DataRow("internal", @"dotnet_code_quality.api_surface = all
                                  dotnet_code_quality.CA1040.api_surface_2 = private")]
        public async Task TestCSharpEmptyInterface_AnalyzerOptions_DiagnosticAsync(string accessibility, string editorConfigText)
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        // General analyzer option
        [DataRow("Public", "dotnet_code_quality.api_surface = Public")]
        [DataRow("Public", "dotnet_code_quality.api_surface = Private, Friend, Public")]
        [DataRow("Public", "dotnet_code_quality.api_surface = All")]
        [DataRow("Protected", "dotnet_code_quality.api_surface = Public")]
        [DataRow("Protected", "dotnet_code_quality.api_surface = Private, Friend, Public")]
        [DataRow("Protected", "dotnet_code_quality.api_surface = All")]
        [DataRow("Friend", "dotnet_code_quality.api_surface = Friend")]
        [DataRow("Friend", "dotnet_code_quality.api_surface = Private, Friend")]
        [DataRow("Friend", "dotnet_code_quality.api_surface = All")]
        [DataRow("Private", "dotnet_code_quality.api_surface = Private")]
        [DataRow("Private", "dotnet_code_quality.api_surface = Private, Public")]
        [DataRow("Private", "dotnet_code_quality.api_surface = All")]
        // Specific analyzer option
        [DataRow("Friend", "dotnet_code_quality.CA1040.api_surface = All")]
        [DataRow("Friend", "dotnet_code_quality.Design.api_surface = All")]
        // General + Specific analyzer option
        [DataRow("Friend", @"dotnet_code_quality.api_surface = Private
                                dotnet_code_quality.CA1040.api_surface = All")]
        // Case-insensitive analyzer option
        [DataRow("Friend", "DOTNET_code_quality.CA1040.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [DataRow("Friend", @"dotnet_code_quality.api_surface = All
                                dotnet_code_quality.CA1040.api_surface_2 = Private")]
        public async Task TestBasicEmptyInterface_AnalyzerOptions_DiagnosticAsync(string accessibility, string editorConfigText)
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("public", "dotnet_code_quality.api_surface = private")]
        [DataRow("public", "dotnet_code_quality.CA1040.api_surface = internal, private")]
        [DataRow("public", "dotnet_code_quality.Design.api_surface = internal, private")]
        [DataRow("public", @"dotnet_code_quality.api_surface = all
                                  dotnet_code_quality.CA1040.api_surface = private")]
        public async Task TestCSharpEmptyInterface_AnalyzerOptions_NoDiagnosticAsync(string accessibility, string editorConfigText)
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("Public", "dotnet_code_quality.api_surface = Private")]
        [DataRow("Public", "dotnet_code_quality.CA1040.api_surface = Friend, Private")]
        [DataRow("Public", "dotnet_code_quality.Design.api_surface = Friend, Private")]
        [DataRow("Public", @"dotnet_code_quality.api_surface = All
                                dotnet_code_quality.CA1040.api_surface = Private")]
        public async Task TestBasicEmptyInterface_AnalyzerOptions_NoDiagnosticAsync(string accessibility, string editorConfigText)
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        // MSTest has no per-row skip; row disabled (was xUnit InlineData Skip): [DataRow(true, Skip = "https://github.com/dotnet/roslyn-analyzers/issues/3494")]
        [DataRow(false)]
        public async Task TestConflictingAnalyzerOptionsForPartialsAsync(bool hasConflict)
        {
            var csTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        { ("/folder1/Test0.cs", @"public partial interface I { }") },
                        { ("/folder2/Test1.cs", @"public partial interface I { }") },
                    },
                    AnalyzerConfigFiles =
                    {
                        ("/folder1/.editorconfig", $"[*.cs]" + Environment.NewLine + "dotnet_code_quality.api_surface = public"),
                        ("/folder2/.editorconfig", $"[*.cs]" + Environment.NewLine + $"dotnet_code_quality.api_surface = {(hasConflict ? "internal" : "public")}"),
                    },
                },
            };

            if (!hasConflict)
            {
                csTest.ExpectedDiagnostics.Add(VerifyCS.Diagnostic().WithSpan(@"/folder1/Test0.cs", 1, 26, 1, 27).WithSpan(@"/folder2/Test1.cs", 1, 26, 1, 27));
            }

            await csTest.RunAsync(CancellationToken.None);

            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        ("/folder1/Test0.vb", @"
Public Partial Interface I
End Interface"),
                        ("/folder2/Test1.vb", @"
Public Partial Interface I
End Interface"),
                    },
                    AnalyzerConfigFiles =
                    {
                        ("/folder1/.editorconfig", $"[*.vb]" + Environment.NewLine + "dotnet_code_quality.api_surface = public"),
                        ("/folder2/.editorconfig", $"[*.vb]" + Environment.NewLine + $"dotnet_code_quality.api_surface = {(hasConflict ? "internal" : "public")}"),
                    },
                },
            };

            if (!hasConflict)
            {
                vbTest.ExpectedDiagnostics.Add(VerifyVB.Diagnostic().WithSpan(@"/folder1/Test0.vb", 2, 26, 2, 27).WithSpan(@"/folder2/Test1.vb", 2, 26, 2, 27));
            }

            await vbTest.RunAsync(CancellationToken.None);
        }

        private static DiagnosticResult CreateCSharpResult(int line, int col)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, col);
#pragma warning restore RS0030 // Do not use banned APIs

        private static DiagnosticResult CreateBasicResult(int line, int col)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, col);
#pragma warning restore RS0030 // Do not use banned APIs
    }
}