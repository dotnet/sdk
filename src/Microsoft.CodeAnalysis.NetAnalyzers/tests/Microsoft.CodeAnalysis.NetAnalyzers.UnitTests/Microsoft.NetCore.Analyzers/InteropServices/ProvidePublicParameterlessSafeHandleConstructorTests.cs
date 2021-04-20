// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.ProvidePublicParameterlessSafeHandleConstructorAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.InteropServices.CSharpProvidePublicParameterlessSafeHandleConstructorFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.ProvidePublicParameterlessSafeHandleConstructorAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.InteropServices.BasicProvidePublicParameterlessSafeHandleConstructorFixer>;

namespace Microsoft.NetCore.Analyzers.InteropServices.UnitTests
{
    public class ProvidePublicParameterlessSafeHandleConstructorTests
    {
        [Fact]
        public async Task NonSafeHandleDerivedType_NoDiagnostics_CS()
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
        public async Task NonSafeHandleDerivedType_NoDiagnostics_VB()
        {
            string source = @"
Class Foo
    Private Sub New()
    End Sub
End Class";
            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task SafeHandleDerivedType_WithParameterlessConstructor_NoDiagnostics_CS()
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
        public async Task SafeHandleDerivedType_WithParameterlessConstructor_NoDiagnostics_VB()
        {
            string source = @"
Imports Microsoft.Win32.SafeHandles
Public Class C : Inherits SafeHandleZeroOrMinusOneIsInvalid
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
        public async Task SafeHandleDerived_WithNonPublicParameterlessConstructor_CodeFix_CS()
        {
            string source = @"
using Microsoft.Win32.SafeHandles;

class FooHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private {|CA1418:FooHandle|}() : base(true)
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
        public async Task SafeHandleDerived_WithNonPublicParameterlessConstructor_CodeFix_VB()
        {
            string source = @"
Imports Microsoft.Win32.SafeHandles
Public Class C : Inherits SafeHandleZeroOrMinusOneIsInvalid

    Private Sub {|CA1418:New|}()
        MyBase.New(True)
    End Sub
    
    Protected Overrides Function ReleaseHandle() As Boolean
        Return True
    End Function

End Class";
            string fixedSource = @"
Imports Microsoft.Win32.SafeHandles
Public Class C : Inherits SafeHandleZeroOrMinusOneIsInvalid

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
        public async Task SafeHandleDerived_WithNoParameterlessConstructor_WithAccessibleBaseTypeParameterlessConstructor_CodeFix_CS()
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
}

class {|CA1418:BarHandle|} : FooHandle
{
    public BarHandle(IntPtr handle)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle() => true;
}";
            string fixedSource = @"
using System;
using Microsoft.Win32.SafeHandles;

abstract class FooHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    protected FooHandle() : base(true)
    {
    }

    protected override bool ReleaseHandle() => true;
}

class BarHandle : FooHandle
{
    public BarHandle(IntPtr handle)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle() => true;

    public BarHandle()
    {
    }
}";

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task SafeHandleDerived_WithNoParameterlessConstructor_WithAccessibleBaseTypeParameterlessConstructor_CodeFix_VB()
        {

        }

        [Fact]
        public async Task SafeHandleDerived_WithNoParameterlessConstructor_WithNoAccessibleBaseTypeParameterlessConstructor_Diagnostic_CS()
        {

        }

        [Fact]
        public async Task SafeHandleDerived_WithNoParameterlessConstructor_WithNoAccessibleBaseTypeParameterlessConstructor_Diagnostic_VB()
        {

        }

        [Fact]
        public async Task SafeHandleDerived_WithNoParameterlessConstructor_WithNoBaseTypeParameterlessConstructor_Diagnostic_CS()
        {

        }

        [Fact]
        public async Task SafeHandleDerived_WithNoParameterlessConstructor_WithNoBaseTypeParameterlessConstructor_Diagnostic_VB()
        {

        }
    }
}
