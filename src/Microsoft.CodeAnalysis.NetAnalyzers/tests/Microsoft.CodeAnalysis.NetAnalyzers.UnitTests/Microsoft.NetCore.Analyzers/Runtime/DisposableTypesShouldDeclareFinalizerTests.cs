// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DisposableTypesShouldDeclareFinalizerAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpDisposableTypesShouldDeclareFinalizerFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DisposableTypesShouldDeclareFinalizerAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicDisposableTypesShouldDeclareFinalizerFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class DisposableTypesShouldDeclareFinalizerTests : DiagnosticAnalyzerTestBase
    {
        [Fact]
        public void CSharpDiagnosticIfIntPtrFieldIsAssignedFromNativeCodeAndNoFinalizerExists()
        {
            var code = @"
using System;
using System.Runtime.InteropServices;

internal static class NativeMethods
{
    [DllImport(""native.dll"")]
    internal static extern IntPtr AllocateResource();
}

public class A : IDisposable
{
    private readonly IntPtr _pi;

    public A()
    {
        _pi = NativeMethods.AllocateResource();
    }

    public void Dispose()
    {
    }
}
";
            VerifyCSharp(code,
                GetCSharpDiagnostic(11, 14));
        }

        [Fact]
        public void BasicDiagnosticIfIntPtrFieldIsAssignedFromNativeCodeAndNoFinalizerExists()
        {
            var code = @"
Imports System
Imports System.Runtime.InteropServices

Friend Class NativeMethods
    <DllImport(""native.dll"")>
    Friend Shared Function AllocateResource() As IntPtr
    End Function
End Class

Public Class A
    Implements IDisposable

    Private ReadOnly _pi As IntPtr

    Public Sub New()
        _pi = NativeMethods.AllocateResource()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class
";
            VerifyBasic(code,
                GetBasicDiagnostic(11, 14));
        }

        [Fact]
        public void CSharpNoDiagnosticIfIntPtrFieldIsAssignedFromNativeCodeAndFinalizerExists()
        {
            var code = @"
using System;
using System.Runtime.InteropServices;

internal static class NativeMethods
{
    [DllImport(""native.dll"")]
    internal static extern IntPtr AllocateResource();
}

public class A : IDisposable
{
    private readonly IntPtr _pi;

    public A()
    {
        _pi = NativeMethods.AllocateResource();
    }

    public void Dispose()
    {
    }

    ~A()
    {
    }
}
";
            VerifyCSharp(code);
        }

        [Fact]
        public void BasicNoDiagnosticIfIntPtrFieldIsAssignedFromNativeCodeAndFinalizerExists()
        {
            var code = @"
Imports System
Imports System.Runtime.InteropServices

Friend Class NativeMethods
    <DllImport(""native.dll"")>
    Friend Shared Function AllocateResource() As IntPtr
    End Function
End Class

Public Class A
    Implements IDisposable

    Private ReadOnly _pi As IntPtr

    Public Sub New()
        _pi = NativeMethods.AllocateResource()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub

    Protected Overrides Sub Finalize()
    End Sub
End Class
";
            VerifyBasic(code);
        }

        [Fact]
        public void CSharpNoDiagnosticIfIntPtrFieldInValueTypeIsAssignedFromNativeCode()
        {
            var code = @"
using System;
using System.Runtime.InteropServices;

internal static class NativeMethods
{
    [DllImport(""native.dll"")]
    internal static extern IntPtr AllocateResource();
}

public struct A : IDisposable // Although disposable structs are evil
{
    private readonly IntPtr _pi;

    public A(int i)
    {
        _pi = NativeMethods.AllocateResource();
    }

    public void Dispose()
    {
    }
}
";
            VerifyCSharp(code);
        }

        [Fact]
        public void BasicNoDiagnosticIfIntPtrFieldInValueTypeIsAssignedFromNativeCode()
        {
            var code = @"
Imports System
Imports System.Runtime.InteropServices

Friend Class NativeMethods
    <DllImport(""native.dll"")>
    Friend Shared Function AllocateResource() As IntPtr
    End Function
End Class

Public Structure A
    Implements IDisposable ' Although disposable structs are evil

    Private ReadOnly _pi As IntPtr

    Public Sub New(i As Integer)
        _pi = NativeMethods.AllocateResource()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Structure
";
            VerifyBasic(code);
        }

        [Fact]
        public void CSharpNoDiagnosticIfIntPtrFieldInNonDisposableTypeIsAssignedFromNativeCode()
        {
            var code = @"
using System;
using System.Runtime.InteropServices;

internal static class NativeMethods
{
    [DllImport(""native.dll"")]
    internal static extern IntPtr AllocateResource();
}

public class A
{
    private readonly IntPtr _pi;

    public A()
    {
        _pi = NativeMethods.AllocateResource();
    }
}
";
            VerifyCSharp(code);
        }

        [Fact]
        public void BasicNoDiagnosticIfIntPtrFieldInNonDisposableTypeIsAssignedFromNativeCode()
        {
            var code = @"
Imports System
Imports System.Runtime.InteropServices

Friend Class NativeMethods
    <DllImport(""native.dll"")>
    Friend Shared Function AllocateResource() As IntPtr
    End Function
End Class

Public Class A
    Private ReadOnly _pi As IntPtr

    Public Sub New()
        _pi = NativeMethods.AllocateResource()
    End Sub

    Public Sub Dispose()
    End Sub
End Class
";
            VerifyBasic(code);
        }

        [Fact]
        public void CSharpNoDiagnosticIfIntPtrFieldIsAssignedFromManagedCode()
        {
            var code = @"
using System;

internal static class ManagedMethods
{
    internal static IntPtr AllocateResource()
    {
        return IntPtr.Zero;
    }
}

public class A : IDisposable
{
    private readonly IntPtr _pi;

    public A()
    {
        _pi = ManagedMethods.AllocateResource();
    }

    public void Dispose()
    {
    }
}
";
            VerifyCSharp(code);
        }

        [Fact]
        public void BasicNoDiagnosticIfIntPtrFieldIsAssignedFromManagedCode()
        {
            var code = @"
Imports System

Friend NotInheritable Class ManagedMethods
    Friend Shared Function AllocateResource() As IntPtr
        Return IntPtr.Zero
    End Function
End Class

Public Class A
    Implements IDisposable

    Private ReadOnly _pi As IntPtr

    Public Sub New()
        _pi = ManagedMethods.AllocateResource()
    End Sub

    Public Overloads Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class
";
            VerifyBasic(code);
        }

        [Fact]
        public void CSharpDiagnosticIfUIntPtrFieldIsAssignedFromNativeCode()
        {
            var code = @"
using System;
using System.Runtime.InteropServices;

internal static class NativeMethods
{
    [DllImport(""native.dll"")]
    internal static extern UIntPtr AllocateResource();
}

public class A : IDisposable
{
    private readonly UIntPtr _pu;

    public A()
    {
        _pu = NativeMethods.AllocateResource();
    }

    public void Dispose()
    {
    }
}
";
            VerifyCSharp(code,
                GetCSharpDiagnostic(11, 14));
        }

        [Fact]
        public void BasicDiagnosticIfUIntPtrFieldIsAssignedFromNativeCode()
        {
            var code = @"
Imports System
Imports System.Runtime.InteropServices

Friend Class NativeMethods
    <DllImport(""native.dll"")>
    Friend Shared Function AllocateResource() As UIntPtr
    End Function
End Class

Public Class A
    Implements IDisposable

    Private ReadOnly _pu As UIntPtr

    Public Sub New()
        _pu = NativeMethods.AllocateResource()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class
";
            VerifyBasic(code,
                GetBasicDiagnostic(11, 14));
        }

        [Fact]
        public void CSharpDiagnosticIfHandleRefFieldIsAssignedFromNativeCode()
        {
            var code = @"
using System;
using System.Runtime.InteropServices;

internal static class NativeMethods
{
    [DllImport(""native.dll"")]
    internal static extern HandleRef AllocateResource();
}

public class A : IDisposable
{
    private readonly HandleRef _hr;

    public A()
    {
        _hr = NativeMethods.AllocateResource();
    }

    public void Dispose()
    {
    }
}
";
            VerifyCSharp(code,
                GetCSharpDiagnostic(11, 14));
        }

        [Fact]
        public void BasicDiagnosticIfHandleRefFieldIsAssignedFromNativeCode()
        {
            var code = @"
Imports System
Imports System.Runtime.InteropServices

Friend Class NativeMethods
    <DllImport(""native.dll"")>
    Friend Shared Function AllocateResource() As HandleRef
    End Function
End Class

Public Class A
    Implements IDisposable

    Private ReadOnly _hr As HandleRef

    Public Sub New()
        _hr = NativeMethods.AllocateResource()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class
";
            VerifyBasic(code,
                GetBasicDiagnostic(11, 14));
        }

        [Fact]
        public void CSharpNoDiagnosticIfNonNativeResourceFieldIsAssignedFromNativeCode()
        {
            var code = @"
using System;
using System.Runtime.InteropServices;

internal static class NativeMethods
{
    [DllImport(""native.dll"")]
    internal static extern int AllocateResource();
}

public class A : IDisposable
{
    private readonly int _i;

    public A()
    {
        _i = NativeMethods.AllocateResource();
    }

    public void Dispose()
    {
    }
}
";
            VerifyCSharp(code);
        }

        [Fact]
        public void BasicNoDiagnosticIfNonNativeResourceFieldIsAssignedFromNativeCode()
        {
            var code = @"
Imports System
Imports System.Runtime.InteropServices

Friend Class NativeMethods
    <DllImport(""native.dll"")>
    Friend Shared Function AllocateResource() As Integer
    End Function
End Class

Public Class A
    Implements IDisposable

    Private ReadOnly _i As Integer

    Public Sub New()
        _i = NativeMethods.AllocateResource()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class
";
            VerifyBasic(code);
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new DisposableTypesShouldDeclareFinalizerAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DisposableTypesShouldDeclareFinalizerAnalyzer();
        }

        private static DiagnosticResult GetCSharpDiagnostic(int line, int column)
        {
            return GetExpectedDiagnostic(line, column);
        }

        private static DiagnosticResult GetBasicDiagnostic(int line, int column)
        {
            return GetExpectedDiagnostic(line, column);
        }

        private static DiagnosticResult GetExpectedDiagnostic(int line, int column)
        {
            return new DiagnosticResult(DisposableTypesShouldDeclareFinalizerAnalyzer.Rule).WithLocation(line, column);
        }
    }
}