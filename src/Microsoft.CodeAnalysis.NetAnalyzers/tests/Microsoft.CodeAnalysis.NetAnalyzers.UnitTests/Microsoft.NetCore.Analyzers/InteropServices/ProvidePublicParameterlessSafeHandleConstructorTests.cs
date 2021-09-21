// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.ProvidePublicParameterlessSafeHandleConstructorAnalyzer,
    Microsoft.NetCore.Analyzers.InteropServices.ProvidePublicParameterlessSafeHandleConstructorFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.ProvidePublicParameterlessSafeHandleConstructorAnalyzer,
    Microsoft.NetCore.Analyzers.InteropServices.ProvidePublicParameterlessSafeHandleConstructorFixer>;

namespace Microsoft.NetCore.Analyzers.InteropServices.UnitTests
{
    public class ProvidePublicParameterlessSafeHandleConstructorTests
    {
        [Fact]
        public async Task NonSafeHandleDerivedType_NoDiagnostics_CSAsync()
        {
            string source = @"
class Foo
{
    private Foo()
    {
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NonSafeHandleDerivedType_NoDiagnostics_VBAsync()
        {
            string source = @"
Class Foo
    Private Sub New()
    End Sub
End Class";
            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task SafeHandleDerivedType_WithParameterlessConstructor_NoDiagnostics_CSAsync()
        {
            string source = @"
using Microsoft.Win32.SafeHandles;

class FooHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public FooHandle() : base(true)
    {
    }

    protected override bool ReleaseHandle() => true;
}";
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task SafeHandleDerivedType_WithParameterlessConstructor_NoDiagnostics_VBAsync()
        {
            string source = @"
Imports Microsoft.Win32.SafeHandles
Public Class FooHandle : Inherits SafeHandleZeroOrMinusOneIsInvalid
    Public Sub New()
        MyBase.New(True)
    End Sub
    
    Protected Overrides Function ReleaseHandle() As Boolean
        Return True
    End Function
End Class";

            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task SafeHandleDerived_WithNonPublicParameterlessConstructor_CodeFix_CSAsync()
        {
            string source = @"
using Microsoft.Win32.SafeHandles;

class FooHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private [|FooHandle|]() : base(true)
    {
    }

    protected override bool ReleaseHandle() => true;
}";
            string fixedSource = @"
using Microsoft.Win32.SafeHandles;

class FooHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public FooHandle() : base(true)
    {
    }

    protected override bool ReleaseHandle() => true;
}";

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task SafeHandleDerived_WithNonPublicParameterlessConstructor_CodeFix_VBAsync()
        {
            string source = @"
Imports Microsoft.Win32.SafeHandles
Public Class FooHandle : Inherits SafeHandleZeroOrMinusOneIsInvalid

    Private Sub [|New|]()
        MyBase.New(True)
    End Sub
    
    Protected Overrides Function ReleaseHandle() As Boolean
        Return True
    End Function

End Class";
            string fixedSource = @"
Imports Microsoft.Win32.SafeHandles
Public Class FooHandle : Inherits SafeHandleZeroOrMinusOneIsInvalid

    Public Sub New()
        MyBase.New(True)
    End Sub
    
    Protected Overrides Function ReleaseHandle() As Boolean
        Return True
    End Function

End Class";

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task SafeHandleDerived_Abstract_NoPublicConstructor_NoDiagnostic_CS()
        {
            string source = @"
using System;
using Microsoft.Win32.SafeHandles;

abstract class FooHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    protected FooHandle() : base(true)
    {
    }

    protected override bool ReleaseHandle() => true;
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task SafeHandleDerived_Abstract_NoPublicConstructor_NoDiagnostic_VBAsync()
        {
            string source = @"
Imports System
Imports Microsoft.Win32.SafeHandles
Public MustInherit Class FooHandle : Inherits SafeHandleZeroOrMinusOneIsInvalid

    Protected Sub New()
        MyBase.New(True)
    End Sub
    
    Protected Overrides Function ReleaseHandle() As Boolean
        Return True
    End Function

End Class";

            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        [WorkItem(5231, "https://github.com/dotnet/roslyn-analyzers/issues/5231")]
        public async Task SafeHandleDerived_WithInternalParameterlessConstructor_InternalType_NoDiagnostic_CSAsync()
        {
            string source = @"
using System;
using Microsoft.Win32.SafeHandles;

internal class BarHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal BarHandle()
        : base(true)
    {
    }

    protected override bool ReleaseHandle() => true;
}";

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        [WorkItem(5231, "https://github.com/dotnet/roslyn-analyzers/issues/5231")]
        public async Task SafeHandleDerived_WithInternalParameterlessConstructor_DefaultAccessibilityType_NoDiagnostic_CSAsync()
        {
            string source = @"
using System;
using Microsoft.Win32.SafeHandles;

class BarHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal BarHandle()
        : base(true)
    {
    }

    protected override bool ReleaseHandle() => true;
}";

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        [WorkItem(5231, "https://github.com/dotnet/roslyn-analyzers/issues/5231")]
        public async Task SafeHandleDerived_WithPrivateProtectedParameterlessConstructor_PrivateProtectedType_NoDiagnostic_CSAsync()
        {
            string source = @"
using System;
using Microsoft.Win32.SafeHandles;

class Containing
{
    private protected class BarHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private protected BarHandle()
            : base(true)
        {
        }

        protected override bool ReleaseHandle() => true;
    }
}";

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }
    }
}
