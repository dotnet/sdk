// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.CallGCSuppressFinalizeCorrectlyAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpCallGCSuppressFinalizeCorrectlyFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.CallGCSuppressFinalizeCorrectlyAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicCallGCSuppressFinalizeCorrectlyFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class CallGCSuppressFinalizeCorrectlyTests
    {
        private const string GCSuppressFinalizeMethodSignature_CSharp = "GC.SuppressFinalize(object)";
        private const string GCSuppressFinalizeMethodSignature_Basic = "GC.SuppressFinalize(Object)";

        private static DiagnosticResult GetCA1816CSharpResultAt(int line, int column, DiagnosticDescriptor rule, string containingMethodName, string gcSuppressFinalizeMethodName) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(containingMethodName, gcSuppressFinalizeMethodName);

        private static DiagnosticResult GetCA1816BasicResultAt(int line, int column, DiagnosticDescriptor rule, string containingMethodName, string gcSuppressFinalizeMethodName) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyVB.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(containingMethodName, gcSuppressFinalizeMethodName);

        #region NoDiagnosticCases

        [Fact]
        public async Task DisposableWithoutFinalizer_CSharp_NoDiagnostic()
        {
            var code = @"
using System;
using System.ComponentModel;

public class DisposableWithoutFinalizer : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        Console.WriteLine(this);
        Console.WriteLine(disposing);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task DisposableWithoutFinalizer_Basic_NoDiagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Public Class DisposableWithoutFinalizer
	Implements IDisposable
	Public Sub Dispose() Implements IDisposable.Dispose
		Dispose(True)
		GC.SuppressFinalize(Me)
	End Sub

	Protected Overridable Sub Dispose(disposing As Boolean)
		Console.WriteLine(Me)
		Console.WriteLine(disposing)
	End Sub
End Class";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task DisposableWithFinalizer_CSharp_NoDiagnostic()
        {
            var code = @"
using System;
using System.ComponentModel;

public class DisposableWithFinalizer : IDisposable
{
    ~DisposableWithFinalizer()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        Console.WriteLine(this);
        Console.WriteLine(disposing);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task DisposableWithFinalizer_Basic_NoDiagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Public Class DisposableWithFinalizer
	Implements IDisposable
	Protected Overrides Sub Finalize()
		Try
			Dispose(False)
		Finally
			MyBase.Finalize()
		End Try
	End Sub

	Public Sub Dispose() Implements IDisposable.Dispose
		Dispose(True)
		GC.SuppressFinalize(Me)
	End Sub

	Protected Overridable Sub Dispose(disposing As Boolean)
		Console.WriteLine(Me)
		Console.WriteLine(disposing)
	End Sub
End Class";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AsyncDisposableWithFinalizer_CSharp_NoDiagnostic()
        {
            var code = @"
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

class MyAsyncDisposable : IAsyncDisposable
{
    [DllImport(""example.dll"")]
    private static extern int GetHandle();

    [DllImport(""example.dll"")]
    private static extern void FreeHandle(int handle);

    private readonly int handle;

    public MyAsyncDisposable()
    {
        this.handle = GetHandle();
    }

    ~MyAsyncDisposable()
    {
        FreeHandle(this.handle);
    }

    public async ValueTask DisposeAsync()
    {
        await Task.Run(() => FreeHandle(this.handle)).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}";
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = code
            }.RunAsync();
        }

        [Fact]
        public async Task SealedDisposableWithoutFinalizer_CSharp_NoDiagnostic()
        {

            var code = @"
using System;
using System.ComponentModel;

public sealed class SealedDisposableWithoutFinalizer : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        Console.WriteLine(this);
        Console.WriteLine(disposing);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task SealedDisposableWithoutFinalizer_Basic_NoDiagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Public NotInheritable Class SealedDisposableWithoutFinalizer
	Implements IDisposable
	Public Sub Dispose() Implements IDisposable.Dispose
		Dispose(True)
		GC.SuppressFinalize(Me)
	End Sub

	Private Sub Dispose(disposing As Boolean)
		Console.WriteLine(Me)
		Console.WriteLine(disposing)
	End Sub
End Class";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task SealedDisposableWithFinalizer_CSharp_NoDiagnostic()
        {
            var code = @"
using System;
using System.ComponentModel;

public sealed class SealedDisposableWithFinalizer : IDisposable
{
    ~SealedDisposableWithFinalizer()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        Console.WriteLine(this);
        Console.WriteLine(disposing);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task SealedDisposableWithFinalizer_Basic_NoDiagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Public NotInheritable Class SealedDisposableWithFinalizer
	Implements IDisposable
	Protected Overrides Sub Finalize()
		Try
			Dispose(False)
		Finally
			MyBase.Finalize()
		End Try
	End Sub

	Public Sub Dispose() Implements IDisposable.Dispose
		Dispose(True)
		GC.SuppressFinalize(Me)
	End Sub

	Private Sub Dispose(disposing As Boolean)
		Console.WriteLine(Me)
		Console.WriteLine(disposing)
	End Sub
End Class";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task InternalDisposableWithoutFinalizer_CSharp_NoDiagnostic()
        {
            var code = @"
using System;
using System.ComponentModel;

internal class InternalDisposableWithoutFinalizer : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        // GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        Console.WriteLine(this);
        Console.WriteLine(disposing);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task InternalDisposableWithoutFinalizer_Basic_NoDiagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Friend Class InternalDisposableWithoutFinalizer
	Implements IDisposable
	Public Sub Dispose() Implements IDisposable.Dispose
		Dispose(True)
		' GC.SuppressFinalize(this);
	End Sub

	Protected Overridable Sub Dispose(disposing As Boolean)
		Console.WriteLine(Me)
		Console.WriteLine(disposing)
	End Sub
End Class";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task PrivateDisposableWithoutFinalizer_CSharp_NoDiagnostic()
        {
            var code = @"
using System;
using System.ComponentModel;

public static class NestedClassHolder
{
    private class PrivateDisposableWithoutFinalizer : IDisposable
    {
        public void Dispose()
        {
            Dispose(true);
            // GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            Console.WriteLine(this);
            Console.WriteLine(disposing);
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task PrivateDisposableWithoutFinalizer_Basic_NoDiagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Public NotInheritable Class NestedClassHolder
	Private Sub New()
	End Sub
	Private Class PrivateDisposableWithoutFinalizer
		Implements IDisposable
		Public Sub Dispose() Implements IDisposable.Dispose
			Dispose(True)
			' GC.SuppressFinalize(this);
		End Sub

		Protected Overridable Sub Dispose(disposing As Boolean)
			Console.WriteLine(Me)
			Console.WriteLine(disposing)
		End Sub
	End Class
End Class";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task SealedDisposableWithoutFinalizerAndWithoutCallingSuppressFinalize_CSharp_NoDiagnostic()
        {
            var code = @"
using System;
using System.ComponentModel;

public sealed class SealedDisposableWithoutFinalizerAndWithoutCallingSuppressFinalize : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        Console.WriteLine(this);
        Console.WriteLine(disposing);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task SealedDisposableWithoutFinalizerAndWithoutCallingSuppressFinalize_Basic_NoDiagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Public NotInheritable Class SealedDisposableWithoutFinalizerAndWithoutCallingSuppressFinalize
	Implements IDisposable
	Public Sub Dispose() Implements IDisposable.Dispose
		Dispose(True)
	End Sub

	Private Sub Dispose(disposing As Boolean)
		Console.WriteLine(Me)
		Console.WriteLine(disposing)
	End Sub
End Class";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task DisposableStruct_CSharp_NoDiagnostic()
        {
            var code = @"
using System;
using System.ComponentModel;

public struct DisposableStruct : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        Console.WriteLine(this);
        Console.WriteLine(disposing);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task DisposableStruct_Basic_NoDiagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Public Structure DisposableStruct
	Implements IDisposable
	Public Sub Dispose() Implements IDisposable.Dispose
		Dispose(True)
	End Sub

	Private Sub Dispose(disposing As Boolean)
		Console.WriteLine(Me)
		Console.WriteLine(disposing)
	End Sub
End Structure";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task SealedDisposableCallingGCSuppressFinalizeInConstructor_CSharp_NoDiagnostic()
        {
            var code = @"
using System;
using System.ComponentModel;

public sealed class SealedDisposableCallingGCSuppressFinalizeInConstructor : Component
{
    public SealedDisposableCallingGCSuppressFinalizeInConstructor()
    {
        // We don't ever want our finalizer (that we inherit from Component) to run
        // (We are sealed and we don't own any unmanaged resources).
        GC.SuppressFinalize(this);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task SealedDisposableCallingGCSuppressFinalizeInConstructor_Basic_NoDiagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Public NotInheritable Class SealedDisposableCallingGCSuppressFinalizeInConstructor
	Inherits Component
	Public Sub New()
		' We don't ever want our finalizer (that we inherit from Component) to run
		' (We are sealed and we don't own any unmanaged resources).
		GC.SuppressFinalize(Me)
	End Sub
End Class";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task Disposable_ImplementedExplicitly_NoDiagnostic()
        {
            var csharpCode = @"
using System;

public class ImplementsDisposableExplicitly : IDisposable
{
    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(csharpCode);

            var vbCode = @"
Imports System

Public Class C
    Implements IDisposable

    Protected Sub NamedDifferent() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Public Sub Dispose(disposing As Boolean)
    End Sub
End Class";
            await VerifyVB.VerifyAnalyzerAsync(vbCode);
        }

        #endregion

        #region DiagnosticCases

        [Fact]
        public async Task SealedDisposableWithFinalizer_CSharp_Diagnostic()
        {
            var code = @"
using System;
using System.ComponentModel;

    public class SealedDisposableWithFinalizer : IDisposable
    {
        public static void Main(string[] args)
        {

        }

        ~SealedDisposableWithFinalizer()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            // GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            Console.WriteLine(this);
            Console.WriteLine(disposing);
        }
    }";
            var diagnosticResult = GetCA1816CSharpResultAt(
                line: 17,
                column: 21,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.NotCalledWithFinalizerRule,
                containingMethodName: "SealedDisposableWithFinalizer.Dispose()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_CSharp);

            await VerifyCS.VerifyAnalyzerAsync(code, diagnosticResult);
        }

        [Fact]
        public async Task SealedDisposableWithFinalizer_Basic_Diagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Public Class SealedDisposableWithFinalizer
	Implements IDisposable
	Public Shared Sub Main(args As String())

	End Sub

	Protected Overrides Sub Finalize()
		Try
			Dispose(False)
		Finally
			MyBase.Finalize()
		End Try
	End Sub

	Public Sub Dispose() Implements IDisposable.Dispose
		Dispose(True)
		' GC.SuppressFinalize(this);
	End Sub

	Private Sub Dispose(disposing As Boolean)
		Console.WriteLine(Me)
		Console.WriteLine(disposing)
	End Sub
End Class";

            var diagnosticResult = GetCA1816BasicResultAt(
                line: 19,
                column: 13,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.NotCalledWithFinalizerRule,
                containingMethodName: "SealedDisposableWithFinalizer.Dispose()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_Basic);

            await VerifyVB.VerifyAnalyzerAsync(code, diagnosticResult);
        }

        [Fact]
        public async Task DisposableWithFinalizer_CSharp_Diagnostic()
        {
            var code = @"
using System;
using System.ComponentModel;

public class DisposableWithFinalizer : IDisposable
{
    ~DisposableWithFinalizer()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        // GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        Console.WriteLine(this);
        Console.WriteLine(disposing);
    }
}";
            var diagnosticResult = GetCA1816CSharpResultAt(
                line: 12,
                column: 17,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.NotCalledWithFinalizerRule,
                containingMethodName: "DisposableWithFinalizer.Dispose()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_CSharp);

            await VerifyCS.VerifyAnalyzerAsync(code, diagnosticResult);
        }

        [Fact]
        public async Task DisposableWithFinalizer_Basic_Diagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Public Class DisposableWithFinalizer
	Implements IDisposable
	Protected Overrides Sub Finalize()
		Try
			Dispose(False)
		Finally
			MyBase.Finalize()
		End Try
	End Sub

	Public Sub Dispose() Implements IDisposable.Dispose
		Dispose(True)
		' GC.SuppressFinalize(this);
	End Sub

	Protected Overridable Sub Dispose(disposing As Boolean)
		Console.WriteLine(Me)
		Console.WriteLine(disposing)
	End Sub
End Class";

            var diagnosticResult = GetCA1816BasicResultAt(
                line: 15,
                column: 13,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.NotCalledWithFinalizerRule,
                containingMethodName: "DisposableWithFinalizer.Dispose()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_Basic);

            await VerifyVB.VerifyAnalyzerAsync(code, diagnosticResult);
        }

        [Fact]
        public async Task InternalDisposableWithFinalizer_CSharp_Diagnostic()
        {
            var code = @"
using System;
using System.ComponentModel;

internal class InternalDisposableWithFinalizer : IDisposable
{
    ~InternalDisposableWithFinalizer()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        // GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        Console.WriteLine(this);
        Console.WriteLine(disposing);
    }
}";
            var diagnosticResult = GetCA1816CSharpResultAt(
                line: 12,
                column: 17,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.NotCalledWithFinalizerRule,
                containingMethodName: "InternalDisposableWithFinalizer.Dispose()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_CSharp);

            await VerifyCS.VerifyAnalyzerAsync(code, diagnosticResult);
        }

        [Fact]
        public async Task InternalDisposableWithFinalizer_Basic_Diagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Friend Class InternalDisposableWithFinalizer
	Implements IDisposable
	Protected Overrides Sub Finalize()
		Try
			Dispose(False)
		Finally
			MyBase.Finalize()
		End Try
	End Sub

	Public Sub Dispose() Implements IDisposable.Dispose
		Dispose(True)
		' GC.SuppressFinalize(this);
	End Sub

	Protected Overridable Sub Dispose(disposing As Boolean)
		Console.WriteLine(Me)
		Console.WriteLine(disposing)
	End Sub
End Class";

            var diagnosticResult = GetCA1816BasicResultAt(
                line: 15,
                column: 13,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.NotCalledWithFinalizerRule,
                containingMethodName: "InternalDisposableWithFinalizer.Dispose()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_Basic);

            await VerifyVB.VerifyAnalyzerAsync(code, diagnosticResult);
        }

        [Fact]
        public async Task PrivateDisposableWithFinalizer_CSharp_Diagnostic()
        {
            var code = @"
using System;
using System.ComponentModel;

public static class NestedClassHolder
{
    private class PrivateDisposableWithFinalizer : IDisposable
    {
        ~PrivateDisposableWithFinalizer()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            // GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            Console.WriteLine(this);
            Console.WriteLine(disposing);
        }
    }
}";
            var diagnosticResult = GetCA1816CSharpResultAt(
                line: 14,
                column: 21,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.NotCalledWithFinalizerRule,
                containingMethodName: "NestedClassHolder.PrivateDisposableWithFinalizer.Dispose()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_CSharp);

            await VerifyCS.VerifyAnalyzerAsync(code, diagnosticResult);
        }

        [Fact]
        public async Task PrivateDisposableWithFinalizer_Basic_Diagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Public NotInheritable Class NestedClassHolder
	Private Sub New()
	End Sub
	Private Class PrivateDisposableWithFinalizer
		Implements IDisposable
		Protected Overrides Sub Finalize()
			Try
				Dispose(False)
			Finally
				MyBase.Finalize()
			End Try
		End Sub

		Public Sub Dispose() Implements IDisposable.Dispose
			Dispose(True)
			' GC.SuppressFinalize(this);
		End Sub

		Protected Overridable Sub Dispose(disposing As Boolean)
			Console.WriteLine(Me)
			Console.WriteLine(disposing)
		End Sub
	End Class
End Class";

            var diagnosticResult = GetCA1816BasicResultAt(
                line: 18,
                column: 14,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.NotCalledWithFinalizerRule,
                containingMethodName: "NestedClassHolder.PrivateDisposableWithFinalizer.Dispose()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_Basic);

            await VerifyVB.VerifyAnalyzerAsync(code, diagnosticResult);
        }

        [Fact]
        public async Task DisposableWithoutFinalizer_CSharp_Diagnostic()
        {
            var code = @"
using System;
using System.ComponentModel;

public class DisposableWithoutFinalizer : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        // GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        Console.WriteLine(this);
        Console.WriteLine(disposing);
    }
}";
            var diagnosticResult = GetCA1816CSharpResultAt(
                line: 7,
                column: 17,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.NotCalledRule,
                containingMethodName: "DisposableWithoutFinalizer.Dispose()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_CSharp);

            await VerifyCS.VerifyAnalyzerAsync(code, diagnosticResult);
        }

        [Fact]
        public async Task DisposableWithoutFinalizer_Basic_Diagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Public Class DisposableWithoutFinalizer
	Implements IDisposable
	Public Sub Dispose() Implements IDisposable.Dispose
		Dispose(True)
		' GC.SuppressFinalize(this);
	End Sub

	Protected Overridable Sub Dispose(disposing As Boolean)
		Console.WriteLine(Me)
		Console.WriteLine(disposing)
	End Sub
End Class";

            var diagnosticResult = GetCA1816BasicResultAt(
                line: 7,
                column: 13,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.NotCalledRule,
                containingMethodName: "DisposableWithoutFinalizer.Dispose()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_Basic);

            await VerifyVB.VerifyAnalyzerAsync(code, diagnosticResult);
        }

        [Fact]
        public async Task DisposableComponent_CSharp_Diagnostic()
        {
            var code = @"
using System;
using System.ComponentModel;

public class DisposableComponent : Component, IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        // GC.SuppressFinalize(this);
    }
}";
            var diagnosticResult = GetCA1816CSharpResultAt(
                line: 7,
                column: 17,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.NotCalledRule,
                containingMethodName: "DisposableComponent.Dispose()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_CSharp);

            await VerifyCS.VerifyAnalyzerAsync(code, diagnosticResult);
        }

        [Fact]
        public async Task DisposableComponent_Basic_Diagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Public Class DisposableComponent
	Inherits Component
	Implements IDisposable
	Public Sub Dispose() Implements IDisposable.Dispose
		Dispose(True)
		' GC.SuppressFinalize(this);
	End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
    End Sub
End Class";

            var diagnosticResult = GetCA1816BasicResultAt(
                line: 8,
                column: 13,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.NotCalledRule,
                containingMethodName: "DisposableComponent.Dispose()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_Basic);

            await VerifyVB.VerifyAnalyzerAsync(code, diagnosticResult);
        }

        [Fact]
        public async Task NotADisposableClass_CSharp_Diagnostic()
        {
            var code = @"
using System;
using System.ComponentModel;

public class NotADisposableClass
{
    public NotADisposableClass()
    {
        GC.SuppressFinalize(this);
    }
}";
            var diagnosticResult = GetCA1816CSharpResultAt(
                line: 9,
                column: 9,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.OutsideDisposeRule,
                containingMethodName: "NotADisposableClass.NotADisposableClass()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_CSharp);

            await VerifyCS.VerifyAnalyzerAsync(code, diagnosticResult);
        }

        [Fact]
        public async Task NotADisposableClass_Basic_Diagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Public Class NotADisposableClass
	Public Sub New()
		GC.SuppressFinalize(Me)
	End Sub
End Class";

            var diagnosticResult = GetCA1816BasicResultAt(
                line: 7,
                column: 3,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.OutsideDisposeRule,
                containingMethodName: "NotADisposableClass.New()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_Basic);

            await VerifyVB.VerifyAnalyzerAsync(code, diagnosticResult);
        }

        [Fact]
        public async Task DisposableClassThatCallsGCSuppressFinalizeInTheWrongPlaces_CSharp_Diagnostic()
        {
            var code = @"
using System;
using System.ComponentModel;

public class DisposableClassThatCallsGCSuppressFinalizeInTheWrongPlaces : IDisposable
{
    public DisposableClassThatCallsGCSuppressFinalizeInTheWrongPlaces()
    {
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        Dispose(true);
        CallGCSuppressFinalize();
    }

    private void CallGCSuppressFinalize()
    {
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Console.WriteLine(this);
            GC.SuppressFinalize(this);
        }
    }
}";
            var diagnosticResult1 = GetCA1816CSharpResultAt(
                line: 9,
                column: 9,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.OutsideDisposeRule,
                containingMethodName: "DisposableClassThatCallsGCSuppressFinalizeInTheWrongPlaces.DisposableClassThatCallsGCSuppressFinalizeInTheWrongPlaces()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_CSharp);
            var diagnosticResult2 = GetCA1816CSharpResultAt(
                line: 12,
                column: 17,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.NotCalledRule,
                containingMethodName: "DisposableClassThatCallsGCSuppressFinalizeInTheWrongPlaces.Dispose()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_CSharp);
            var diagnosticResult3 = GetCA1816CSharpResultAt(
                line: 20,
                column: 9,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.OutsideDisposeRule,
                containingMethodName: "DisposableClassThatCallsGCSuppressFinalizeInTheWrongPlaces.CallGCSuppressFinalize()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_CSharp);
            var diagnosticResult4 = GetCA1816CSharpResultAt(
                line: 28,
                column: 13,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.OutsideDisposeRule,
                containingMethodName: "DisposableClassThatCallsGCSuppressFinalizeInTheWrongPlaces.Dispose(bool)",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_CSharp);

            await VerifyCS.VerifyAnalyzerAsync(code, diagnosticResult1, diagnosticResult2, diagnosticResult3, diagnosticResult4);
        }

        [Fact]
        public async Task DisposableClassThatCallsGCSuppressFinalizeInTheWrongPlaces_Basic_Diagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Public Class DisposableClassThatCallsGCSuppressFinalizeInTheWrongPlaces
	Implements IDisposable
	Public Sub New()
		GC.SuppressFinalize(Me)
	End Sub

	Public Sub Dispose() Implements IDisposable.Dispose
		Dispose(True)
		CallGCSuppressFinalize()
	End Sub

	Private Sub CallGCSuppressFinalize()
		GC.SuppressFinalize(Me)
	End Sub

	Protected Overridable Sub Dispose(disposing As Boolean)
		If disposing Then
			Console.WriteLine(Me)
			GC.SuppressFinalize(Me)
		End If
	End Sub
End Class";

            var diagnosticResult1 = GetCA1816BasicResultAt(
                line: 8,
                column: 3,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.OutsideDisposeRule,
                containingMethodName: "DisposableClassThatCallsGCSuppressFinalizeInTheWrongPlaces.New()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_Basic);
            var diagnosticResult2 = GetCA1816BasicResultAt(
                line: 11,
                column: 13,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.NotCalledRule,
                containingMethodName: "DisposableClassThatCallsGCSuppressFinalizeInTheWrongPlaces.Dispose()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_Basic);
            var diagnosticResult3 = GetCA1816BasicResultAt(
                line: 17,
                column: 3,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.OutsideDisposeRule,
                containingMethodName: "DisposableClassThatCallsGCSuppressFinalizeInTheWrongPlaces.CallGCSuppressFinalize()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_Basic);
            var diagnosticResult4 = GetCA1816BasicResultAt(
                line: 23,
                column: 4,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.OutsideDisposeRule,
                containingMethodName: "DisposableClassThatCallsGCSuppressFinalizeInTheWrongPlaces.Dispose(Boolean)",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_Basic);

            await VerifyVB.VerifyAnalyzerAsync(code, diagnosticResult1, diagnosticResult2, diagnosticResult3, diagnosticResult4);
        }

        [Fact]
        public async Task DisposableClassThatCallsGCSuppressFinalizeWithTheWrongArguments_CSharp_Diagnostic()
        {
            var code = @"
using System;
using System.ComponentModel;

public class DisposableClassThatCallsGCSuppressFinalizeWithTheWrongArguments : IDisposable
{
    public DisposableClassThatCallsGCSuppressFinalizeWithTheWrongArguments()
    {
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(true);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Console.WriteLine(this);
        }
    }
}";
            var diagnosticResult = GetCA1816CSharpResultAt(
                line: 14,
                column: 9,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.NotPassedThisRule,
                containingMethodName: "DisposableClassThatCallsGCSuppressFinalizeWithTheWrongArguments.Dispose()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_CSharp);

            await VerifyCS.VerifyAnalyzerAsync(code, diagnosticResult);
        }

        [Fact]
        public async Task DisposableClassThatCallsGCSuppressFinalizeWithTheWrongArguments_Basic_Diagnostic()
        {
            var code = @"
Imports System
Imports System.ComponentModel

Public Class DisposableClassThatCallsGCSuppressFinalizeWithTheWrongArguments
	Implements IDisposable
	Public Sub New()
	End Sub

	Public Sub Dispose() Implements IDisposable.Dispose
		Dispose(True)
		GC.SuppressFinalize(True)
	End Sub

	Protected Overridable Sub Dispose(disposing As Boolean)
		If disposing Then
			Console.WriteLine(Me)
		End If
	End Sub
End Class";
            var diagnosticResult = GetCA1816BasicResultAt(
                line: 12,
                column: 3,
                rule: CallGCSuppressFinalizeCorrectlyAnalyzer.NotPassedThisRule,
                containingMethodName: "DisposableClassThatCallsGCSuppressFinalizeWithTheWrongArguments.Dispose()",
                gcSuppressFinalizeMethodName: GCSuppressFinalizeMethodSignature_Basic);

            await VerifyVB.VerifyAnalyzerAsync(code, diagnosticResult);
        }

        #endregion
    }
}
