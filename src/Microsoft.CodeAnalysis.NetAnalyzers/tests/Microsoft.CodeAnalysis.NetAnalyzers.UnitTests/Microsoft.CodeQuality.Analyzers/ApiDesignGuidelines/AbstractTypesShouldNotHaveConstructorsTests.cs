// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AbstractTypesShouldNotHaveConstructorsAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AbstractTypesShouldNotHaveConstructorsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AbstractTypesShouldNotHaveConstructorsAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AbstractTypesShouldNotHaveConstructorsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class CA1012Tests
    {
        [Fact]
        public async Task TestCSPublicAbstractClass()
        {
            var code = @"
public abstract class C
{
    public C()
    {
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCA1012CSharpResultAt(2, 23, "C"));
        }

        [Fact]
        public async Task TestVBPublicAbstractClass()
        {
            var code = @"
Public MustInherit Class C
    Public Sub New()
    End Sub
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetCA1012BasicResultAt(2, 26, "C"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TestCSInternalAbstractClass()
        {
            var code = @"
abstract class C
{
    public C()
    {
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TestVBInternalAbstractClass()
        {
            var code = @"
MustInherit Class C
    Public Sub New()
    End Sub
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestCSAbstractClassWithProtectedConstructor()
        {
            var code = @"
public abstract class C
{
    protected C()
    {
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestVBAbstractClassWithProtectedConstructor()
        {
            var code = @"
Public MustInherit Class C
    Protected Sub New()
    End Sub
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TestCSNestedAbstractClassWithPublicConstructor1()
        {
            var code = @"
public struct C
{
    abstract class D
    {
        public D() { }
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestVBNestedAbstractClassWithPublicConstructor1()
        {
            var code = @"
Public Structure C
    MustInherit Class D
        Public Sub New()
        End Sub
    End Class
End Structure
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetCA1012BasicResultAt(3, 23, "D"));
        }

        [Fact]
        public async Task TestNestedAbstractClassWithPublicConstructor2()
        {
            var code = @"
public abstract class C
{
    public abstract class D
    {
        public D() { }
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCA1012CSharpResultAt(4, 27, "D"));
        }

        [Fact]
        public async Task TestVBNestedAbstractClassWithPublicConstructor2()
        {
            var code = @"
Public MustInherit Class C
   Protected Friend MustInherit Class D
        Sub New()
        End Sub
    End Class
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetCA1012BasicResultAt(3, 39, "D"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TestNestedAbstractClassWithPublicConstructor3()
        {
            var code = @"
internal abstract class C
{
    public abstract class D
    {
        public D() { }
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TestVBNestedAbstractClassWithPublicConstructor3()
        {
            var code = @"
MustInherit Class C
   Public MustInherit Class D
        Sub New()
        End Sub
    End Class
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        private static DiagnosticResult GetCA1012CSharpResultAt(int line, int column, string objectName)
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
                .WithArguments(objectName);

        private static DiagnosticResult GetCA1012BasicResultAt(int line, int column, string objectName)
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
                .WithArguments(objectName);
    }
}
