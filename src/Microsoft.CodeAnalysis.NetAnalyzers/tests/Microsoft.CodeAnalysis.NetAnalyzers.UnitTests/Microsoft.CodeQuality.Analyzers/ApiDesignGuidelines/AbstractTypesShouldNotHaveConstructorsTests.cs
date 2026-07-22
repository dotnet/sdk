// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AbstractTypesShouldNotHaveConstructorsAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AbstractTypesShouldNotHaveConstructorsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AbstractTypesShouldNotHaveConstructorsAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AbstractTypesShouldNotHaveConstructorsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    [TestClass]
    public class CA1012Tests
    {
        [TestMethod]
        public async Task TestCSPublicAbstractClassAsync()
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

        [TestMethod]
        public async Task TestVBPublicAbstractClassAsync()
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

        [TestMethod, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TestCSInternalAbstractClassAsync()
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

        [TestMethod, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TestVBInternalAbstractClassAsync()
        {
            var code = @"
MustInherit Class C
    Public Sub New()
    End Sub
End Class
";
            await VerifyVB.VerifyCodeFixAsync(code, code);
        }

        [TestMethod]
        public async Task TestCSAbstractClassWithProtectedConstructorAsync()
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

        [TestMethod]
        public async Task TestVBAbstractClassWithProtectedConstructorAsync()
        {
            var code = @"
Public MustInherit Class C
    Protected Sub New()
    End Sub
End Class
";
            await VerifyVB.VerifyCodeFixAsync(code, code);
        }

        [TestMethod, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TestCSNestedAbstractClassWithPublicConstructor1Async()
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

        [TestMethod]
        public async Task TestVBNestedAbstractClassWithPublicConstructor1Async()
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

        [TestMethod]
        public async Task TestNestedAbstractClassWithPublicConstructor2Async()
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

        [TestMethod]
        public async Task TestVBNestedAbstractClassWithPublicConstructor2Async()
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

        [TestMethod, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TestNestedAbstractClassWithPublicConstructor3Async()
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

        [TestMethod, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task TestVBNestedAbstractClassWithPublicConstructor3Async()
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
