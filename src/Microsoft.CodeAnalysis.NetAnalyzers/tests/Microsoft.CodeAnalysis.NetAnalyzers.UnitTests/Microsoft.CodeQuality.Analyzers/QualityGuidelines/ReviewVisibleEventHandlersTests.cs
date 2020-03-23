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
                GetCSharpDefaultResultAt(6, 17, "Handler1"),
                GetCSharpDefaultResultAt(7, 20, "Handler2"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class Program
    Public Sub Handler1(ByVal sender As Object, ByVal args As EventArgs)
    End Sub

    Protected Sub Handler2(ByVal sender As Object, ByVal args As EventArgs)
    End Sub
End Class",
                GetBasicDefaultResultAt(5, 16, "Handler1"),
                GetBasicDefaultResultAt(8, 19, "Handler2"));
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
    public void Handler1(object sender, EventArgs args) {}
}",
                GetCSharpSecurityResultAt(8, 17, "Handler1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Security.Permissions

Public Class Program
    <SecurityPermissionAttribute(SecurityAction.Demand, UnmanagedCode:=True)>
    Public Sub Handler1(ByVal sender As Object, ByVal args As EventArgs)
    End Sub
End Class",
                GetBasicSecurityResultAt(7, 16, "Handler1"));
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
    public void Handler1(object sender, Windows.UI.Xaml.RoutedEventArgs args) {}
}",
                GetCSharpDefaultResultAt(9, 17, "Handler1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Namespace Windows.UI.Xaml
    Public Class RoutedEventArgs
    End Class
End Namespace

Public Class Program
    Public Sub Handler1(ByVal sender As Object, ByVal args As Windows.UI.Xaml.RoutedEventArgs)
    End Sub
End Class",
                GetBasicDefaultResultAt(8, 16, "Handler1"));
        }

        private static DiagnosticResult GetCSharpDefaultResultAt(int line, int column, string methodName)
            => VerifyCS.Diagnostic(ReviewVisibleEventHandlersAnalyzer.DefaultRule)
                .WithLocation(line, column)
                .WithArguments(methodName);

        private static DiagnosticResult GetCSharpSecurityResultAt(int line, int column, string methodName)
            => VerifyCS.Diagnostic(ReviewVisibleEventHandlersAnalyzer.SecurityRule)
                .WithLocation(line, column)
                .WithArguments(methodName);

        private static DiagnosticResult GetBasicDefaultResultAt(int line, int column, string methodName)
            => VerifyVB.Diagnostic(ReviewVisibleEventHandlersAnalyzer.DefaultRule)
                .WithLocation(line, column)
                .WithArguments(methodName);

        private static DiagnosticResult GetBasicSecurityResultAt(int line, int column, string methodName)
            => VerifyVB.Diagnostic(ReviewVisibleEventHandlersAnalyzer.SecurityRule)
                .WithLocation(line, column)
                .WithArguments(methodName);
    }
}