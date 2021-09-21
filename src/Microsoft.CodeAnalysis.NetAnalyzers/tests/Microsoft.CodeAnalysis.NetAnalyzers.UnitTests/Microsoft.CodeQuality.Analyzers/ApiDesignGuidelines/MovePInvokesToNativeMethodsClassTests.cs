// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MovePInvokesToNativeMethodsClassAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpMovePInvokesToNativeMethodsClassFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MovePInvokesToNativeMethodsClassAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicMovePInvokesToNativeMethodsClassFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class MovePInvokesToNativeMethodsClassTests
    {
        #region Verifiers

        private static DiagnosticResult CSharpResult(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic().WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult BasicResult(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic().WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        #endregion

        [Fact]
        public async Task CA1060ProperlyNamedClassCSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

class NativeMethods
{
    [DllImport(""user32.dll"")]
    private static extern void SomeExternMethod();
}

class SafeNativeMethods
{
    [DllImport(""user32.dll"")]
    private static extern void SomeExternMethod();
}

class UnsafeNativeMethods
{
    [DllImport(""user32.dll"")]
    private static extern void SomeExternMethod();
}
");
        }

        [Fact]
        public async Task CA1060ProperlyNamedClassBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Class NativeMethods
    <DllImport(""user32.dll"")>
    Private Shared Sub SomeExternMethod()
    End Sub
End Class

Class SafeNativeMethods
    <DllImport(""user32.dll"")>
    Private Shared Sub SomeExternMethod()
    End Sub
End Class

Class UnsafeNativeMethods
    <DllImport(""user32.dll"")>
    Private Shared Sub SomeExternMethod()
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1060ImproperlyNamedClassCSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

class FirstClass
{
    [DllImport(""user32.dll"")]
    private static extern void SomeExternMethod();
}

class SecondClass
{
    [DllImport(""user32.dll"")]
    private static extern void SomeExternMethod();
}

class ThirdClass
{
    [DllImport(""user32.dll"")]
    private static extern void SomeExternMethod();
}
",
            CSharpResult(4, 7),
            CSharpResult(10, 7),
            CSharpResult(16, 7));
        }

        [Fact]
        public async Task CA1060ImproperlyNamedClassCSharpWithScopeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

class [|FirstClass|]
{
    [DllImport(""user32.dll"")]
    private static extern void SomeExternMethod();
}

class [|SecondClass|]
{
    [DllImport(""user32.dll"")]
    private static extern void SomeExternMethod();
}

class [|ThirdClass|]
{
    [DllImport(""user32.dll"")]
    private static extern void SomeExternMethod();
}
");
        }

        [Fact]
        public async Task CA1060ImproperlyNamedClassBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Class FirstClass
    <DllImport(""user32.dll"")>
    Private Shared Sub SomeExternMethod()
    End Sub
End Class

Class SecondClass
    <DllImport(""user32.dll"")>
    Private Shared Sub SomeExternMethod()
    End Sub
End Class

Class ThirdClass
    <DllImport(""user32.dll"")>
    Private Shared Sub SomeExternMethod()
    End Sub
End Class
",
            BasicResult(4, 7),
            BasicResult(10, 7),
            BasicResult(16, 7));
        }

        [Fact]
        public async Task CA1060ImproperlyNamedClassBasicWithScopeAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Class [|FirstClass|]
    <DllImport(""user32.dll"")>
    Private Shared Sub SomeExternMethod()
    End Sub
End Class

Class [|SecondClass|]
    <DllImport(""user32.dll"")>
    Private Shared Sub SomeExternMethod()
    End Sub
End Class

Class [|ThirdClass|]
    <DllImport(""user32.dll"")>
    Private Shared Sub SomeExternMethod()
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1060ClassesInNamespaceCSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

namespace MyNamespace
{
    class NativeMethods
    {
        [DllImport(""user32.dll"")]
        private static extern void SomeExternMethod();
    }

    class SomeClass
    {
        [DllImport(""user32.dll"")]
        private static extern void SomeExternMethod();
    }
}
",
            CSharpResult(12, 11));
        }

        [Fact]
        public async Task CA1060ClassesInNamespaceBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Namespace MyNamespace
    Class NativeMethods
        <DllImport(""user32.dll"")>
        Private Shared Sub SomeExternMethod()
        End Sub
    End Class

    Class SomeClass
        <DllImport(""user32.dll"")>
        Private Shared Sub SomeExternMethod()
        End Sub
    End Class
End Namespace
",
            BasicResult(11, 11));
        }

        [Fact]
        public async Task CA1060NestedClassesCSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

class Outer
{
    class SomeClass
    {
        [DllImport(""user32.dll"")]
        private static extern void SomeExternMethod();
    }
}
",
            CSharpResult(6, 11));
        }

        [Fact]
        public async Task CA1060NestedClassesBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Class Outer
    Class SomeClass
        <DllImport(""user32.dll"")>
        Private Shared Sub SomeExternMethod()
        End Sub
    End Class
End Class
",
            BasicResult(5, 11));
        }
    }
}
