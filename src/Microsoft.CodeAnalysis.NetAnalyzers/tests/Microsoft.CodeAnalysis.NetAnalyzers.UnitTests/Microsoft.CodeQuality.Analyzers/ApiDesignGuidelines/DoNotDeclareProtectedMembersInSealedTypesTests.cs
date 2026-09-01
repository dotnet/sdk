// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDeclareProtectedMembersInSealedTypes,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDeclareProtectedMembersInSealedTypes,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    [TestClass]
    public class DoNotDeclareProtectedMembersInSealedTypesTests
    {

        [TestMethod]
        public async Task ProtectedSubInNotInheritable_DiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public NotInheritable Class C
    Protected Sub M()
    End Sub
End Class",
                VerifyVB.Diagnostic().WithSpan(3, 19, 3, 20).WithArguments("M", "C"));
        }

        [TestMethod]
        [DataRow("protected")]
        [DataRow("protected internal")]
        [DataRow("private protected")]
        public Task AnyProtectedVariantMembersInSealed_DiagnosticAsync(string accessModifier)
        {
            return new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
using System;

public sealed class C
{{
    {accessModifier} int [|SomeField|];

    {accessModifier} int [|SomeProperty|] {{ [|get|]; [|set|]; }}

    {accessModifier} event EventHandler [|SomeEvent|];

    {accessModifier} void [|SomeMethod|]() {{ }}
}}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
dotnet_code_quality.CA1047.api_surface = All
") }
                }
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("Protected")]
        [DataRow("Protected Friend")]
        [DataRow("Private Protected")]
        public Task AnyProtectedVariantMemberInNotInheritable_DiagnosticAsync(string accessModifier)
        {
            return new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { $@"
Imports System

Public NotInheritable Class C
    {accessModifier} [|SomeField|] As Integer

    {accessModifier} Property [|SomeProperty|] As Integer

    {accessModifier} Event [|SomeEvent|] As EventHandler

    {accessModifier} Sub [|SomeSub|]()
    End Sub

    {accessModifier} Function [|SomeFunction|]() As Integer
    End Function
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
dotnet_code_quality.CA1047.api_surface = All
") }
                }
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task ProtectedOverridesMemberInNotInheritable_NoDiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Protected Overridable Property SomeProperty As Integer

    Protected Overridable Sub SomeSub()
    End Sub

    Protected Overridable Function SomeFunction() As Integer
    End Function
End Class

Public NotInheritable Class C2
    Inherits C
    Protected Overrides Property SomeProperty As Integer

    Protected Overrides Sub SomeSub()
    End Sub

    Protected Overrides Function SomeFunction() As Integer
    End Function
End Class");
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
        [DataRow("Friend", "dotnet_code_quality.CA1047.api_surface = All")]
        [DataRow("Friend", "dotnet_code_quality.Design.api_surface = All")]
        // General + Specific analyzer option
        [DataRow("Friend", @"dotnet_code_quality.api_surface = Private
                                dotnet_code_quality.CA1047.api_surface = All")]
        // Case-insensitive analyzer option
        [DataRow("Friend", "DOTNET_code_quality.CA1047.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [DataRow("Friend", @"dotnet_code_quality.api_surface = All
                                dotnet_code_quality.CA1047.api_surface_2 = Private")]
        public async Task VisualBasic_ApiSurfaceOptionAsync(string accessibility, string editorConfigText)
        {
            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Public Class OuterClass
    {accessibility} NotInheritable Class C
        Protected [|SomeField|] As Integer
    End Class
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task Finalize_NoDiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public NotInheritable Class C
    Protected Overrides Sub Finalize()
    End Sub
End Class");
        }
    }
}
