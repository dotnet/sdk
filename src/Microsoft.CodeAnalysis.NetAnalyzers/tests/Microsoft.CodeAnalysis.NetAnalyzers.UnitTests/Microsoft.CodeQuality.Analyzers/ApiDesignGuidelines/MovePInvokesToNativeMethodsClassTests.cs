// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MovePInvokesToNativeMethodsClassAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpMovePInvokesToNativeMethodsClassFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MovePInvokesToNativeMethodsClassAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicMovePInvokesToNativeMethodsClassFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class MovePInvokesToNativeMethodsClassTests : DiagnosticAnalyzerTestBase
    {
        #region Verifiers

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new MovePInvokesToNativeMethodsClassAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new MovePInvokesToNativeMethodsClassAnalyzer();
        }

        private static DiagnosticResult CSharpResult(int line, int column)
            => VerifyCS.Diagnostic().WithLocation(line, column);

        private static DiagnosticResult BasicResult(int line, int column)
            => VerifyVB.Diagnostic().WithLocation(line, column);

        #endregion

        [Fact]
        public async Task CA1060ProperlyNamedClassCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

class NativeMethods
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}

class SafeNativeMethods
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}

class UnsafeNativeMethods
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}
");
        }

        [Fact]
        public async Task CA1060ProperlyNamedClassBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Class NativeMethods
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class

Class SafeNativeMethods
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class

Class UnsafeNativeMethods
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1060ImproperlyNamedClassCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

class FooClass
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}

class BarClass
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}

class BazClass
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}
",
            CSharpResult(4, 7),
            CSharpResult(10, 7),
            CSharpResult(16, 7));
        }

        [Fact]
        public void CA1060ImproperlyNamedClassCSharpWithScope()
        {
            VerifyCSharp(@"
using System.Runtime.InteropServices;

class FooClass
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}

[|class BarClass
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}|]

class BazClass
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}
",
            CSharpResult(10, 7));
        }

        [Fact]
        public async Task CA1060ImproperlyNamedClassBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Class FooClass
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class

Class BarClass
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class

Class BazClass
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class
",
            BasicResult(4, 7),
            BasicResult(10, 7),
            BasicResult(16, 7));
        }

        [Fact]
        public void CA1060ImproperlyNamedClassBasicWithScope()
        {
            VerifyBasic(@"
Imports System.Runtime.InteropServices

Class FooClass
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class

[|Class BarClass
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class|]

Class BazClass
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class
",
            BasicResult(10, 7));
        }

        [Fact]
        public async Task CA1060ClassesInNamespaceCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

namespace MyNamespace
{
    class NativeMethods
    {
        [DllImport(""user32.dll"")]
        private static extern void Foo();
    }

    class BarClass
    {
        [DllImport(""user32.dll"")]
        private static extern void Foo();
    }
}
",
            CSharpResult(12, 11));
        }

        [Fact]
        public async Task CA1060ClassesInNamespaceBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Namespace MyNamespace
    Class NativeMethods
        <DllImport(""user32.dll"")>
        Private Shared Sub Foo()
        End Sub
    End Class

    Class BarClass
        <DllImport(""user32.dll"")>
        Private Shared Sub Foo()
        End Sub
    End Class
End Namespace
",
            BasicResult(11, 11));
        }

        [Fact]
        public async Task CA1060NestedClassesCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

class Outer
{
    class BarClass
    {
        [DllImport(""user32.dll"")]
        private static extern void Foo();
    }
}
",
            CSharpResult(6, 11));
        }

        [Fact]
        public async Task CA1060NestedClassesBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Class Outer
    Class BarClass
        <DllImport(""user32.dll"")>
        Private Shared Sub Foo()
        End Sub
    End Class
End Class
",
            BasicResult(5, 11));
        }
    }
}
