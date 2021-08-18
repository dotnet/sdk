// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DisposableTypesShouldDeclareFinalizerAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpDisposableTypesShouldDeclareFinalizerFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DisposableTypesShouldDeclareFinalizerAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicDisposableTypesShouldDeclareFinalizerFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class DisposableTypesShouldDeclareFinalizerTests
    {
        [Fact]
        public async Task CSharpDiagnosticIfIntPtrFieldIsAssignedFromNativeCodeAndNoFinalizerExistsAsync()
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
            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpDiagnostic(11, 14));
        }

        [Fact]
        public async Task BasicDiagnosticIfIntPtrFieldIsAssignedFromNativeCodeAndNoFinalizerExistsAsync()
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
            await VerifyVB.VerifyAnalyzerAsync(code,
                GetBasicDiagnostic(11, 14));
        }

        [Fact]
        public async Task CSharpNoDiagnosticIfIntPtrFieldIsAssignedFromNativeCodeAndFinalizerExistsAsync()
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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticIfIntPtrFieldIsAssignedFromNativeCodeAndFinalizerExistsAsync()
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
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpNoDiagnosticIfIntPtrFieldInValueTypeIsAssignedFromNativeCodeAsync()
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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticIfIntPtrFieldInValueTypeIsAssignedFromNativeCodeAsync()
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
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpNoDiagnosticIfIntPtrFieldInNonDisposableTypeIsAssignedFromNativeCodeAsync()
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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticIfIntPtrFieldInNonDisposableTypeIsAssignedFromNativeCodeAsync()
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
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpNoDiagnosticIfIntPtrFieldIsAssignedFromManagedCodeAsync()
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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticIfIntPtrFieldIsAssignedFromManagedCodeAsync()
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
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpDiagnosticIfUIntPtrFieldIsAssignedFromNativeCodeAsync()
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
            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpDiagnostic(11, 14));
        }

        [Fact]
        public async Task BasicDiagnosticIfUIntPtrFieldIsAssignedFromNativeCodeAsync()
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
            await VerifyVB.VerifyAnalyzerAsync(code,
                GetBasicDiagnostic(11, 14));
        }

        [Fact]
        public async Task CSharpDiagnosticIfHandleRefFieldIsAssignedFromNativeCodeAsync()
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
            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpDiagnostic(11, 14));
        }

        [Fact]
        public async Task BasicDiagnosticIfHandleRefFieldIsAssignedFromNativeCodeAsync()
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
            await VerifyVB.VerifyAnalyzerAsync(code,
                GetBasicDiagnostic(11, 14));
        }

        [Fact]
        public async Task CSharpNoDiagnosticIfNonNativeResourceFieldIsAssignedFromNativeCodeAsync()
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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticIfNonNativeResourceFieldIsAssignedFromNativeCodeAsync()
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
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        private static DiagnosticResult GetCSharpDiagnostic(int line, int column) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyCS.Diagnostic().WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetBasicDiagnostic(int line, int column) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyVB.Diagnostic().WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}