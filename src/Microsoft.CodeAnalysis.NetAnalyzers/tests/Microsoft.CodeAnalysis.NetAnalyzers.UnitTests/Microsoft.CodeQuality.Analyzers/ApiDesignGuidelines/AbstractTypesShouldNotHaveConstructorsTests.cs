// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
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
public abstract class [|C|]
{
    public C()
    {
    }
}
";
            var fix = @"
public abstract class C
{
    protected C()
    {
    }
}
";
            await VerifyCS.VerifyCodeFixAsync(code, fix);
        }

        [Fact]
        public async Task TestVBPublicAbstractClass()
        {
            var code = @"
Public MustInherit Class [|C|]
    Public Sub New()
    End Sub
End Class
";
            var fix = @"
Public MustInherit Class C
    Protected Sub New()
    End Sub
End Class
";
            await VerifyVB.VerifyCodeFixAsync(code, fix);
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
            await VerifyCS.VerifyCodeFixAsync(code, code);
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
            await VerifyVB.VerifyCodeFixAsync(code, code);
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
            await VerifyCS.VerifyCodeFixAsync(code, code);
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
            await VerifyVB.VerifyCodeFixAsync(code, code);
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
            await VerifyCS.VerifyCodeFixAsync(code, code);
        }

        [Fact]
        public async Task TestVBNestedAbstractClassWithPublicConstructor1()
        {
            var code = @"
Public Structure C
    MustInherit Class [|D|]
        Public Sub New()
        End Sub
    End Class
End Structure
";
            var fix = @"
Public Structure C
    MustInherit Class D
        Protected Sub New()
        End Sub
    End Class
End Structure
";
            await VerifyVB.VerifyCodeFixAsync(code, fix);
        }

        [Fact]
        public async Task TestNestedAbstractClassWithPublicConstructor2()
        {
            var code = @"
public abstract class C
{
    public abstract class [|D|]
    {
        public D() { }
    }
}
";
            var fix = @"
public abstract class C
{
    public abstract class D
    {
        protected D() { }
    }
}
";
            await VerifyCS.VerifyCodeFixAsync(code, fix);
        }

        [Fact]
        public async Task TestVBNestedAbstractClassWithPublicConstructor2()
        {
            var code = @"
Public MustInherit Class C
   Protected Friend MustInherit Class [|D|]
        Sub New()
        End Sub
    End Class
End Class
";
            var fix = @"
Public MustInherit Class C
   Protected Friend MustInherit Class D
        Protected Sub New()
        End Sub
    End Class
End Class
";
            await VerifyVB.VerifyCodeFixAsync(code, fix);
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
            await VerifyCS.VerifyCodeFixAsync(code, code);
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
            await VerifyVB.VerifyCodeFixAsync(code, code);
        }
    }
}
