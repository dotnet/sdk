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
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic().WithLocation(6, 17).WithArguments("Handler1"),
#pragma warning restore RS0030 // Do not used banned APIs
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic().WithLocation(7, 20).WithArguments("Handler2"));
#pragma warning restore RS0030 // Do not used banned APIs

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class Program
    Public Sub Handler1(ByVal sender As Object, ByVal args As EventArgs)
    End Sub

    Protected Sub Handler2(ByVal sender As Object, ByVal args As EventArgs)
    End Sub
End Class",
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyVB.Diagnostic().WithLocation(5, 16).WithArguments("Handler1"),
#pragma warning restore RS0030 // Do not used banned APIs
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyVB.Diagnostic().WithLocation(8, 19).WithArguments("Handler2"));
#pragma warning restore RS0030 // Do not used banned APIs
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

        [Fact]
        public async Task CA2109_PrivateInternalEventHandler_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class Program
{
    private void Handler1(object sender, EventArgs args) {}
    internal void Handler2(object sender, EventArgs args) {}
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class Program
    Private Sub Handler1(ByVal sender As Object, ByVal args As EventArgs)
    End Sub

    Friend Sub Handler2(ByVal sender As Object, ByVal args As EventArgs)
    End Sub
End Class");
        }

        [Fact]
        public async Task CA2109_PublicProtectedNotEventHandler_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class Program
{
    public void Handler1(object sender) {}
    public void Handler2(object sender, object o) {}
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class Program
    Public Sub Handler1(ByVal sender As Object)
    End Sub

    Public Sub Handler2(ByVal sender As Object, ByVal o As Object)
    End Sub
End Class");
        }

        [Fact]
        public async Task CA2109_PublicOverrideVirtualEventHandler_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class A
{
    public virtual void [|Handler1|](object sender, EventArgs args) {}
}

public class B : A
{
    public override void Handler1(object sender, EventArgs args) {}
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class A
    Public Overridable Sub [|Handler1|](ByVal sender As Object, ByVal args As EventArgs)
    End Sub
End Class

Public Class B
    Inherits A

    Public Overrides Sub Handler1(ByVal sender As Object, ByVal args As EventArgs)
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA2109_PublicOverrideAbstractEventHandler_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public abstract class A
{
    public abstract void [|Handler1|](object sender, EventArgs args);
}

public class B : A
{
    public override void Handler1(object sender, EventArgs args) {}
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public MustInherit Class A
    Public MustOverride Sub [|Handler1|](ByVal sender As Object, ByVal args As EventArgs)
End Class

Public Class B
    Inherits A

    Public Overrides Sub Handler1(ByVal sender As Object, ByVal args As EventArgs)
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA2109_PublicInterfaceImplementationEventHandler_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public interface IA
{
    void [|Handler1|](object sender, EventArgs args);
}

public class B : IA
{
    public void Handler1(object sender, EventArgs args) {}
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Interface IA
    Sub [|Handler1|](ByVal sender As Object, ByVal args As EventArgs)
End Interface

Public Class B
    Implements IA

    Public Sub Handler1(ByVal sender As Object, ByVal args As EventArgs) Implements IA.Handler1
    End Sub
End Class");
        }
    }
}