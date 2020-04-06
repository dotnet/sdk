// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.ReviewVisibleEventHandlersAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.ReviewVisibleEventHandlersAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class ReviewVisibleEventHandlersTests
    {
        [Fact]
        public async Task CA2109_PublicEventHandler_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class Program
{
    public void Handler1(object sender, EventArgs args) {}
    protected void Handler2(object sender, EventArgs args) {}
}",
                GetCSharpResultAt(6, 17, "Handler1"),
                GetCSharpResultAt(7, 20, "Handler2"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class Program
    Public Sub Handler1(ByVal sender As Object, ByVal args As EventArgs)
    End Sub

    Protected Sub Handler2(ByVal sender As Object, ByVal args As EventArgs)
    End Sub
End Class",
                GetBasicResultAt(5, 16, "Handler1"),
                GetBasicResultAt(8, 19, "Handler2"));
        }

        [Fact]
        public async Task CA2109_PublicEventHandlerWithSecurityAttribute_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Permissions;

public class Program
{
    [SecurityPermissionAttribute(SecurityAction.Demand, UnmanagedCode=true)]
    public void [|Handler1|](object sender, EventArgs args) {}
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Security.Permissions

Public Class Program
    <SecurityPermissionAttribute(SecurityAction.Demand, UnmanagedCode:=True)>
    Public Sub [|Handler1|](ByVal sender As Object, ByVal args As EventArgs)
    End Sub
End Class");
        }

        [Fact]
        public async Task CA2109_PublicEventHandlerUWP_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace Windows.UI.Xaml
{
    public class RoutedEventArgs {}
}

public class Program
{
    public void [|Handler1|](object sender, Windows.UI.Xaml.RoutedEventArgs args) {}
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Namespace Windows.UI.Xaml
    Public Class RoutedEventArgs
    End Class
End Namespace

Public Class Program
    Public Sub [|Handler1|](ByVal sender As Object, ByVal args As Windows.UI.Xaml.RoutedEventArgs)
    End Sub
End Class");
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
        [InlineData("internal", "dotnet_code_quality.CA2109.api_surface = all")]
        [InlineData("internal", "dotnet_code_quality.Security.api_surface = all")]
        // General + Specific analyzer option
        [InlineData("internal", @"dotnet_code_quality.api_surface = private
                                  dotnet_code_quality.CA2109.api_surface = all")]
        // Case-insensitive analyzer option
        [InlineData("internal", "DOTNET_code_quality.CA2109.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [InlineData("internal", @"dotnet_code_quality.api_surface = all
                                  dotnet_code_quality.CA2109.api_surface_2 = private")]
        public async Task CSharp_ApiSurfaceOption(string accessibility, string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
using System;
public class OuterClass
{{
    public class C
    {{
        {accessibility} void [|Handler1|](object sender, EventArgs args) {{}}
    }}
}}"
                    },
                    AdditionalFiles = { (".editorconfig", editorConfigText), },
                },
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
        [InlineData("Friend", "dotnet_code_quality.CA2109.api_surface = All")]
        [InlineData("Friend", "dotnet_code_quality.Security.api_surface = All")]
        // General + Specific analyzer option
        [InlineData("Friend", @"dotnet_code_quality.api_surface = Private
                                dotnet_code_quality.CA2109.api_surface = All")]
        // Case-insensitive analyzer option
        [InlineData("Friend", "DOTNET_code_quality.CA2109.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [InlineData("Friend", @"dotnet_code_quality.api_surface = All
                                dotnet_code_quality.CA2109.api_surface_2 = Private")]
        public async Task VisualBasic_ApiSurfaceOption(string accessibility, string editorConfigText)
        {
            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Imports System
Public Class OuterClass
    Public Class C
        {accessibility} Sub [|Handler1|](ByVal sender As Object, ByVal args As EventArgs)
        End Sub
    End Class
End Class"
                    },
                    AdditionalFiles = { (".editorconfig", editorConfigText), },
                },
            }.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, string methodName)
            => VerifyCS.Diagnostic(ReviewVisibleEventHandlersAnalyzer.Rule)
                .WithLocation(line, column)
                .WithArguments(methodName);

        private static DiagnosticResult GetBasicResultAt(int line, int column, string methodName)
            => VerifyVB.Diagnostic(ReviewVisibleEventHandlersAnalyzer.Rule)
                .WithLocation(line, column)
                .WithArguments(methodName);
    }
}