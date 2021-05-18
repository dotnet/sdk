// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDeclareProtectedMembersInSealedTypes,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDeclareProtectedMembersInSealedTypes,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class DoNotDeclareProtectedMembersInSealedTypesTests
    {

        [Fact]
        public async Task ProtectedSubInNotInheritable_Diagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public NotInheritable Class C
    Protected Sub M()
    End Sub
End Class",
                VerifyVB.Diagnostic().WithSpan(3, 19, 3, 20).WithArguments("M", "C"));
        }

        [Theory]
        [InlineData("protected")]
        [InlineData("protected internal")]
        [InlineData("private protected")]
        public Task AnyProtectedVariantMembersInSealed_Diagnostic(string accessModifier)
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("Protected")]
        [InlineData("Protected Friend")]
        [InlineData("Private Protected")]
        public Task AnyProtectedVariantMemberInNotInheritable_Diagnostic(string accessModifier)
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
            }.RunAsync();
        }

        [Fact]
        public async Task ProtectedOverridesMemberInNotInheritable_NoDiagnostic()
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
        [InlineData("Friend", "dotnet_code_quality.CA1047.api_surface = All")]
        [InlineData("Friend", "dotnet_code_quality.Design.api_surface = All")]
        // General + Specific analyzer option
        [InlineData("Friend", @"dotnet_code_quality.api_surface = Private
                                dotnet_code_quality.CA1047.api_surface = All")]
        // Case-insensitive analyzer option
        [InlineData("Friend", "DOTNET_code_quality.CA1047.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [InlineData("Friend", @"dotnet_code_quality.api_surface = All
                                dotnet_code_quality.CA1047.api_surface_2 = Private")]
        public async Task VisualBasic_ApiSurfaceOption(string accessibility, string editorConfigText)
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
            }.RunAsync();
        }

        [Fact]
        public async Task Finalize_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public NotInheritable Class C
    Protected Overrides Sub Finalize()
    End Sub
End Class");
        }
    }
}
