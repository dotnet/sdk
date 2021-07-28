// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpForwardCancellationTokenToInvocationsAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpForwardCancellationTokenToInvocationsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicForwardCancellationTokenToInvocationsAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicForwardCancellationTokenToInvocationsFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class ForwardCancellationTokenToInvocationsTests
    {
        #region No Diagnostic - C#

        [Fact]
        public Task CS_NoDiagnostic_NoParentToken_AsyncNoToken()
        {
            return VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M()
    {
        await MethodAsync();
    }
    Task MethodAsync() => Task.CompletedTask;
}
            ");
        }

        [Fact]
        public Task CS_NoDiagnostic_NoParentToken_SyncNoToken()
        {
            return VerifyCS.VerifyAnalyzerAsync(@"
class C
{
    void M()
    {
        MyMethod();
    }
    void MyMethod() {}
}
            ");
        }

        [Fact]
        public Task CS_NoDiagnostic_NoParentToken_TokenDefault()
        {
            return VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M()
    {
        await MethodAsync();
    }
    Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ");
        }

        [Fact]
        public Task CS_NoDiagnostic_NoToken()
        {
            return VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync();
    }
    Task MethodAsync() => Task.CompletedTask;
}
            ");
        }

        [Fact]
        public Task CS_NoDiagnostic_OverloadArgumentsDontMatch()
        {
            return VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(5, ""Hello world"");
    }
    Task MethodAsync(int i, string s) => Task.CompletedTask;
    Task MethodAsync(int i, CancellationToken ct) => Task.CompletedTask;
}
            ");
        }

        [Fact]
        public Task CS_NoDiagnostic_Overload_AlreadyPassingToken()
        {
            return VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ");
        }

        [Fact]
        public Task CS_NoDiagnostic_Default_AlreadyPassingToken()
        {
            return VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;
class C
{
    void M(CancellationToken ct)
    {
        Method(ct);
    }
    void Method(CancellationToken c = default) {}
}
            ");
        }

        [Fact]
        public Task CS_NoDiagnostic_PassingTokenFromSource()
        {
            return VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        CancellationTokenSource cts = new CancellationTokenSource();
        await MethodAsync(cts.Token);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ");
        }

        [Fact]
        public Task CS_NoDiagnostic_PassingExplicitDefault()
        {
            return VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(default);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ");
        }

        [Fact]
        public Task CS_NoDiagnostic_PassingExplicitDefaultCancellationToken()
        {
            return VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(default(CancellationToken));
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ");
        }

        [Fact]
        public Task CS_NoDiagnostic_PassingExplicitCancellationTokenNone()
        {
            return VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(CancellationToken.None);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ");
        }

        [Fact]
        public Task CS_NoDiagnostic_OverloadTokenNotLastParameter()
        {
            return VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync();
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(int x, CancellationToken ct, string s) => Task.CompletedTask;
}
            ");
        }

        [Fact]
        public Task CS_NoDiagnostic_OverloadWithMultipleTokens()
        {
            return VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync();
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c1, CancellationToken ct2) => Task.CompletedTask;
}
            ");
        }

        [Fact]
        public Task CS_NoDiagnostic_OverloadWithMultipleTokensSeparated()
        {
            return VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync();
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(int x, CancellationToken c1, string s, CancellationToken ct2) => Task.CompletedTask;
}
            ");
        }

        [Fact]
        public Task CS_NoDiagnostic_NamedTokenUnordered()
        {
            return VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(s: ""Hello world"", c: CancellationToken.None, x: 5);
    }
    Task MethodAsync(int x, string s, CancellationToken c) => Task.CompletedTask;
}
            ");
        }

        [Fact]
        public Task CS_NoDiagnostic_Overload_NamedTokenUnordered()
        {
            return VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(s: ""Hello world"", c: CancellationToken.None, x: 5);
    }
    Task MethodAsync(int x, string s) => Task.CompletedTask;
    Task MethodAsync(int x, string s, CancellationToken c) => Task.CompletedTask;
}
            ");
        }

        [Fact]
        public Task CS_NoDiagnostic_CancellationTokenSource_ParamsUsed_Order()
        {
            /*
            CancellationTokenSource has 3 different overloads that take CancellationToken arguments.
            We should detect if a ct is passed and not offer a diagnostic, because it's considered one of the `params`.

            public class CancellationTokenSource : IDisposable
            {
                public static CancellationTokenSource CreateLinkedTokenSource(CancellationToken token);
                public static CancellationTokenSource CreateLinkedTokenSource(CancellationToken token1, CancellationToken token2);
                public static CancellationTokenSource CreateLinkedTokenSource(params CancellationToken[] tokens);
            }
            */
            return CS8VerifyAnalyzerAsync(@"
using System.Threading;
class C
{
    void M(CancellationToken ct)
    {
        CTS.Method(ct); // Don't diagnose
    }
}
class CTS
{
    public static void Method(CancellationToken token){}
    public static void Method(CancellationToken token1, CancellationToken token2){}
    public static void Method(params CancellationToken[] tokens){}
}
            ");
        }

        [Fact]
        public Task CS_NoDiagnostic_ExtensionMethodTakesToken()
        {
            // The extension method is in another class, make sure the object mc is not substituted with the static class name
            string originalCode = @"
using System;
using System.Threading;
class C
{
    public void M(CancellationToken ct)
    {
        MyClass mc = new MyClass();
        mc.MyMethod();
    }
}
public class MyClass
{
    public void MyMethod() { }
}
public static class Extensions
{
    public static void MyMethod(this MyClass mc, CancellationToken c) { }
}
            ";
            return CS8VerifyAnalyzerAsync(originalCode);
        }

        [Fact]
        [WorkItem(3786, "https://github.com/dotnet/roslyn-analyzers/issues/3786")]
        public Task CS_NoDiagnostic_ParametersDifferMoreThanOne()
        {
            return CS8VerifyAnalyzerAsync(@"
using System;
using System.Threading;
class C
{
    void MyMethod(int i) {}
    void MyMethod(int i, bool b) {}
    void MyMethod(int i, bool b, CancellationToken c) {}

    public void M(CancellationToken ct)
    {
        MyMethod(1);
    }
}
            ");
        }

        [Fact]
        [WorkItem(3786, "https://github.com/dotnet/roslyn-analyzers/issues/3786")]
        public Task CS_NoDiagnostic_LambdaAndExtensionMethod_NoTokenInLambda()
        {
            // Only for local methods will we look for the ct in the top-most ancestor
            // For anonymous methods we will only look in the immediate ancestor
            return VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Threading;
public static class Extensions
{
    public static void Extension(this bool b, Action<int> action) {}
    public static void MyMethod(this int i, CancellationToken c = default) {}
}
class C
{
    public void M(CancellationToken ct)
    {
        bool b = false;
        b.Extension((j) =>
        {
            j.MyMethod();
        });
    }
}
            ");
        }

        [Fact]
        [WorkItem(3786, "https://github.com/dotnet/roslyn-analyzers/issues/3786")]
        public Task CS_NoDiagnostic_AnonymousDelegateAndExtensionMethod_NoTokenInAnonymousDelegate()
        {
            // Only for local methods will we look for the ct in the top-most ancestor
            // For anonymous methods we will only look in the immediate ancestor
            return VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Threading;
public static class Extensions
{
    public delegate void MyDelegate(int i);
    public static void Extension(this bool b, MyDelegate d) {}
    public static void MyMethod(this int i, CancellationToken c = default) {}
}
class C
{
    public void M(CancellationToken ct)
    {
        bool b = false;
        b.Extension((int j) =>
        {
            j.MyMethod();
        });
    }
}
            ");
        }

        [Fact]
        [WorkItem(4985, "https://github.com/dotnet/roslyn-analyzers/issues/4985")]
        public Task CS_NoDiagnostic_ReturnTypesDiffer()
        {
            return VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Threading;
using System.Threading.Tasks;

class P
{
    static void M1(string s, CancellationToken cancellationToken)
    {
        var result = M2(s);
    }

    static Task M2(string s) { throw new NotImplementedException(); }

    static int M2(string s, CancellationToken cancellationToken) { throw new NotImplementedException(); }
}
            ");
        }

        #endregion

        #region Diagnostics with no fix = C#

        [Fact]
        public Task CS_AnalyzerOnlyDiagnostic_OverloadWithNamedParametersUnordered()
        {
            // This is a special case that will get a diagnostic but will not get a fix
            // because the fixer does not currently have a way to know the overload's ct parameter name
            // If the ct argument got added at the end without a name, compilation would fail with:
            // CA8323: Named argument 'z' is used out-of-position but is followed by an unnamed argument
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    Task M(CancellationToken ct)
    {
        return [|MethodAsync|](z: ""Hello world"", x: 5, y: true);
    }
    Task MethodAsync(int x, bool y = default, string z = """") => Task.CompletedTask;
    Task MethodAsync(int x, bool y = default, string z = """", CancellationToken c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyAnalyzerAsync(originalCode);
        }

        [Fact]
        public Task CS_AnalyzerOnlyDiagnostic_CancellationTokenSource_ParamsEmpty()
        {
            /*
            CancellationTokenSource has 3 different overloads that take CancellationToken arguments.
            When no ct is passed, because the overload that takes one instance is not setting a default value, then the analyzer considers it the `params`.

            public class CancellationTokenSource : IDisposable
            {
                public static CancellationTokenSource CreateLinkedTokenSource(CancellationToken token);
                public static CancellationTokenSource CreateLinkedTokenSource(CancellationToken token1, CancellationToken token2);
                public static CancellationTokenSource CreateLinkedTokenSource(params CancellationToken[] tokens);
            }

            In C#, the invocation for a static method includes the type and the dot
            */
            string originalCode = @"
using System.Threading;
class C
{
    void M(CancellationToken ct)
    {
        CancellationTokenSource cts = [|CancellationTokenSource.CreateLinkedTokenSource|]();
    }
}
            ";
            return CS8VerifyAnalyzerAsync(originalCode);
        }

        [Fact]
        [WorkItem(3786, "https://github.com/dotnet/roslyn-analyzers/issues/3786")]
        public Task CS_AnalyzerOnlyDiagnostic_StaticLocalMethod()
        {
            // Local static functions are available in C# >= 8.0
            // The user should fix convert the static local method into a non-static local method,
            // or pass `default` or `CancellationToken.None` manually
            string originalCode = @"
using System;
using System.Threading;
class C
{
    public static void MyMethod(int i, CancellationToken c = default) {}
    public void M(CancellationToken ct)
    {
        LocalStaticMethod();
        static void LocalStaticMethod()
        {
            [|MyMethod|](5);
        }
    }
}
            ";
            return CS8VerifyAnalyzerAsync(originalCode);
        }

        [Fact]
        [WorkItem(3786, "https://github.com/dotnet/roslyn-analyzers/issues/3786")]
        public Task CS_AnalyzerOnlyDiagnostic_LocalMethod_InsideOf_StaticLocalMethod_TokenInTopParent()
        {
            // Local static functions are available in C# >= 8.0
            // The user should fix convert the static local method into a non-static local method,
            // or pass `default` or `CancellationToken.None` manually
            string originalCode = @"
using System;
using System.Threading;
class C
{
    public static void MyMethod(int i, CancellationToken c = default) {}
    public void M(CancellationToken ct)
    {
        LocalStaticMethod();
        static void LocalStaticMethod()
        {
            LocalMethod();
            void LocalMethod()
            {
                [|MyMethod|](5);
            }
        }
    }
}
            ";
            return CS8VerifyAnalyzerAsync(originalCode);
        }

        #endregion

        #region Diagnostics with fix = C#

        [Fact]
        public Task CS_Diagnostic_Class_TokenDefault()
        {
            string originalCode = @"
using System.Threading;
class C
{
    void M(CancellationToken ct)
    {
        [|MyMethod|]();
    }
    int MyMethod(CancellationToken c = default) => 1;
}
            ";
            string fixedCode = @"
using System.Threading;
class C
{
    void M(CancellationToken ct)
    {
        MyMethod(ct);
    }
    int MyMethod(CancellationToken c = default) => 1;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_Class_TokenDefault_WithConfigureAwait()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await [|MethodAsync|]().ConfigureAwait(false);
    }
    Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(ct).ConfigureAwait(false);
    }
    Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_NoAwait()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        [|MethodAsync|]();
    }
    Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        MethodAsync(ct);
    }
    Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_SaveTask()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        Task t = [|MethodAsync|]();
    }
    Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        Task t = MethodAsync(ct);
    }
    Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_ClassStaticMethod_TokenDefault()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await [|MethodAsync|]();
    }
    static Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(ct);
    }
    static Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_ClassStaticMethod_TokenDefault_WithConfigureAwait()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await [|MethodAsync|]().ConfigureAwait(false);
    }
    static Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(ct).ConfigureAwait(false);
    }
    static Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_OtherClass_TokenDefault()
        {
            string originalCode = @"
using System.Threading;
class C
{
    void M(CancellationToken ct)
    {
        O o = new O();
        [|o.MyMethod|]();
    }
}
class O
{
    public int MyMethod(CancellationToken c = default) => 1;
}
            ";
            string fixedCode = @"
using System.Threading;
class C
{
    void M(CancellationToken ct)
    {
        O o = new O();
        o.MyMethod(ct);
    }
}
class O
{
    public int MyMethod(CancellationToken c = default) => 1;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_OtherClass_TokenDefault_WithConfigureAwait()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        O o = new O();
        await [|o.MethodAsync|]();
    }
}
class O
{
    public Task MethodAsync() => Task.CompletedTask;
    public Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        O o = new O();
        await o.MethodAsync(ct);
    }
}
class O
{
    public Task MethodAsync() => Task.CompletedTask;
    public Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_OtherClassStaticMethod_TokenDefault()
        {
            // The invocation for a static method includes the type and the dot
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await [|O.MethodAsync|]();
    }
}
class O
{
    public static Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await O.MethodAsync(ct);
    }
}
class O
{
    public static Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_OtherClassStaticMethod_TokenDefault_WithConfigureAwait()
        {
            // The invocation for a static method includes the type and the dot
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await [|O.MethodAsync|]();
    }
}
class O
{
    static public Task MethodAsync() => Task.CompletedTask;
    static public Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await O.MethodAsync(ct);
    }
}
class O
{
    static public Task MethodAsync() => Task.CompletedTask;
    static public Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_Struct_TokenDefault()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
struct S
{
    async void M(CancellationToken ct)
    {
        await [|MethodAsync|]();
    }
    Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
struct S
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(ct);
    }
    Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_Struct_TokenDefault_WithConfigureAwait()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
struct S
{
    async void M(CancellationToken ct)
    {
        await [|MethodAsync|]().ConfigureAwait(false);
    }
    Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
struct S
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(ct).ConfigureAwait(false);
    }
    Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_OverloadToken()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await [|MethodAsync|]();
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_OverloadToken_WithConfigureAwait()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await [|MethodAsync|]();
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_OverloadTokenDefault()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await [|MethodAsync|]();
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_OverloadTokenDefault_WithConfigureAwait()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await [|MethodAsync|]().ConfigureAwait(false);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(ct).ConfigureAwait(false);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_OverloadsArgumentsMatch()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await [|MethodAsync|](5, ""Hello world"");
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
    Task MethodAsync(int x, string s) => Task.CompletedTask;
    Task MethodAsync(int x, string s, CancellationToken ct) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(5, ""Hello world"", ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
    Task MethodAsync(int x, string s) => Task.CompletedTask;
    Task MethodAsync(int x, string s, CancellationToken ct) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_OverloadsArgumentsMatch_WithConfigureAwait()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await [|MethodAsync|](5, ""Hello world"").ConfigureAwait(true);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
    Task MethodAsync(int x, string s) => Task.CompletedTask;
    Task MethodAsync(int x, string s, CancellationToken ct) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(5, ""Hello world"", ct).ConfigureAwait(true);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
    Task MethodAsync(int x, string s) => Task.CompletedTask;
    Task MethodAsync(int x, string s, CancellationToken ct) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_ActionDelegateAwait()
        {
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        Action<CancellationToken> a = async (CancellationToken token) => await [|MethodAsync|]();
        a(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        Action<CancellationToken> a = async (CancellationToken token) => await MethodAsync(token);
        a(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_ActionDelegateNoAwait()
        {
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        Action<CancellationToken> a = (CancellationToken c) => [|MethodAsync|]();
        a(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        Action<CancellationToken> a = (CancellationToken c) => MethodAsync(c);
        a(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_ActionDelegateAwait_WithConfigureAwait()
        {
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        Action<CancellationToken> a = async (CancellationToken token) => await [|MethodAsync|]().ConfigureAwait(false);
        a(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        Action<CancellationToken> a = async (CancellationToken token) => await MethodAsync(token).ConfigureAwait(false);
        a(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_FuncDelegateAwait()
        {
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        Func<CancellationToken, Task<bool>> f = async (CancellationToken token) =>
        {
            await [|MethodAsync|]();
            return true;
        };
        f(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        Func<CancellationToken, Task<bool>> f = async (CancellationToken token) =>
        {
            await MethodAsync(token);
            return true;
        };
        f(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_FuncDelegateAwait_WithConfigureAwait()
        {
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        Func<CancellationToken, Task<bool>> f = async (CancellationToken token) =>
        {
            await [|MethodAsync|]().ConfigureAwait(true);
            return true;
        };
        f(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        Func<CancellationToken, Task<bool>> f = async (CancellationToken token) =>
        {
            await MethodAsync(token).ConfigureAwait(true);
            return true;
        };
        f(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_FuncDelegateAwaitOutside()
        {
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        Func<CancellationToken, Task> f = (CancellationToken c) => [|MethodAsync|]();
        await f(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        Func<CancellationToken, Task> f = (CancellationToken c) => MethodAsync(c);
        await f(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_NestedFunctionAwait()
        {
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        async void LocalMethod(CancellationToken token)
        {
            await [|MethodAsync|]();
        }
        LocalMethod(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        async void LocalMethod(CancellationToken token)
        {
            await MethodAsync(token);
        }
        LocalMethod(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_NestedFunctionNoAwait()
        {
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        void LocalMethod(CancellationToken token)
        {
            [|MethodAsync|]();
        }
        LocalMethod(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        void LocalMethod(CancellationToken token)
        {
            MethodAsync(token);
        }
        LocalMethod(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_NestedFunctionAwaitOutside()
        {
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        Task LocalMethod(CancellationToken token)
        {
            return [|MethodAsync|]();
        }
        await LocalMethod(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        Task LocalMethod(CancellationToken token)
        {
            return MethodAsync(token);
        }
        await LocalMethod(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_NestedFunctionAwait_WithConfigureAwait()
        {
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        async void LocalMethod(CancellationToken token)
        {
            await [|MethodAsync|]().ConfigureAwait(false);
        }
        LocalMethod(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    void M(CancellationToken ct)
    {
        async void LocalMethod(CancellationToken token)
        {
            await MethodAsync(token).ConfigureAwait(false);
        }
        LocalMethod(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_AliasTokenInDefault()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
using TokenAlias = System.Threading.CancellationToken;
class C
{
    async void M(CancellationToken ct)
    {
        await [|MethodAsync|]();
    }
    Task MethodAsync(TokenAlias c = default) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
using TokenAlias = System.Threading.CancellationToken;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(ct);
    }
    Task MethodAsync(TokenAlias c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_AliasTokenInOverload()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
using TokenAlias = System.Threading.CancellationToken;
class C
{
    async void M(CancellationToken ct)
    {
        await [|MethodAsync|]();
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(TokenAlias c) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
using TokenAlias = System.Threading.CancellationToken;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(TokenAlias c) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_Default_AliasTokenInMethodParameter()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
using TokenAlias = System.Threading.CancellationToken;
class C
{
    async void M(TokenAlias ct)
    {
        await [|MethodAsync|]();
    }
    Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
using TokenAlias = System.Threading.CancellationToken;
class C
{
    async void M(TokenAlias ct)
    {
        await MethodAsync(ct);
    }
    Task MethodAsync(CancellationToken c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_Overload_AliasTokenInMethodParameter()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
using TokenAlias = System.Threading.CancellationToken;
class C
{
    async void M(TokenAlias ct)
    {
        await [|MethodAsync|]();
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
using TokenAlias = System.Threading.CancellationToken;
class C
{
    async void M(TokenAlias ct)
    {
        await MethodAsync(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_Default_AliasTokenInDefaultAndMethodParameter()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
using TokenAlias = System.Threading.CancellationToken;
class C
{
    async void M(TokenAlias ct)
    {
        await [|MethodAsync|]();
    }
    Task MethodAsync(TokenAlias c = default) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
using TokenAlias = System.Threading.CancellationToken;
class C
{
    async void M(TokenAlias ct)
    {
        await MethodAsync(ct);
    }
    Task MethodAsync(TokenAlias c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_Overload_AliasTokenInOverloadAndMethodParameter()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
using TokenAlias = System.Threading.CancellationToken;
class C
{
    async void M(TokenAlias ct)
    {
        await [|MethodAsync|]();
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
using TokenAlias = System.Threading.CancellationToken;
class C
{
    async void M(TokenAlias ct)
    {
        await MethodAsync(ct);
    }
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_Default_WithAllDefaultParametersImplicit()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    Task M(CancellationToken ct)
    {
        return [|MethodAsync|]();
    }
    Task MethodAsync(int x = 0, bool y = false, CancellationToken c = default)
    {
        return Task.CompletedTask;
    }
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    Task M(CancellationToken ct)
    {
        return MethodAsync(c: ct);
    }
    Task MethodAsync(int x = 0, bool y = false, CancellationToken c = default)
    {
        return Task.CompletedTask;
    }
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_Default_WithSomeDefaultParameters()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await [|MethodAsync|](5);
    }
    Task MethodAsync(int x, bool y = default, CancellationToken c = default) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(5, c: ct);
    }
    Task MethodAsync(int x, bool y = default, CancellationToken c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_Default_WithNamedParameters()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await [|MethodAsync|](x: 5);
    }
    Task MethodAsync(int x, bool y = default, CancellationToken c = default) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(x: 5, c: ct);
    }
    Task MethodAsync(int x, bool y = default, CancellationToken c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_Default_WithAncestorAliasAndNamedParameters()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
using TokenAlias = System.Threading.CancellationToken;
class C
{
    async void M(TokenAlias ct)
    {
        await [|MethodAsync|](x: 5);
    }
    Task MethodAsync(int x, bool y = default, CancellationToken c = default) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
using TokenAlias = System.Threading.CancellationToken;
class C
{
    async void M(TokenAlias ct)
    {
        await MethodAsync(x: 5, c: ct);
    }
    Task MethodAsync(int x, bool y = default, CancellationToken c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_Default_WithMethodArgumentAliasAndNamedParameters()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
using TokenAlias = System.Threading.CancellationToken;
class C
{
    async void M(CancellationToken ct)
    {
        await [|MethodAsync|](x: 5);
    }
    Task MethodAsync(int x, bool y = default, TokenAlias c = default) => Task.CompletedTask;
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
using TokenAlias = System.Threading.CancellationToken;
class C
{
    async void M(CancellationToken ct)
    {
        await MethodAsync(x: 5, c: ct);
    }
    Task MethodAsync(int x, bool y = default, TokenAlias c = default) => Task.CompletedTask;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_Default_WithNamedParametersUnordered()
        {
            string originalCode = @"
using System.Threading;
class C
{
    int M(CancellationToken ct)
    {
        return [|MyMethod|](z: ""Hello world"", x: 5, y: true);
    }
    int MyMethod(int x, bool y = default, string z = """", CancellationToken c = default) => 1;
}
            ";
            // Notice the parameters do NOT get reordered to their official position
            string fixedCode = @"
using System.Threading;
class C
{
    int M(CancellationToken ct)
    {
        return MyMethod(z: ""Hello world"", x: 5, y: true, c: ct);
    }
    int MyMethod(int x, bool y = default, string z = """", CancellationToken c = default) => 1;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_WithLock()
        {
            string originalCode = @"
using System.Threading;
class C
{
    private readonly object lockingObject = new object();
    int M (CancellationToken ct)
    {
        int x;
        lock (lockingObject)
        {
            x = [|MyMethod|](5);
        }
        return x;
    }
    int MyMethod(int x, CancellationToken c = default) => 1;
}
            ";
            string fixedCode = @"
using System.Threading;
class C
{
    private readonly object lockingObject = new object();
    int M (CancellationToken ct)
    {
        int x;
        lock (lockingObject)
        {
            x = MyMethod(5, ct);
        }
        return x;
    }
    int MyMethod(int x, CancellationToken c = default) => 1;
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_DereferencePossibleNullReference()
        {
            string originalCode = @"
#nullable enable
using System.Threading;
class C
{
    O? PossiblyNull()
    {
        return null;
    }
    void M(CancellationToken ct)
    {
        O? o = PossiblyNull();
        o?.[|MyMethod|]();
    }
}
class O
{
    public int MyMethod(CancellationToken c = default) => 1;
}
            ";
            string fixedCode = @"
#nullable enable
using System.Threading;
class C
{
    O? PossiblyNull()
    {
        return null;
    }
    void M(CancellationToken ct)
    {
        O? o = PossiblyNull();
        o?.MyMethod(ct);
    }
}
class O
{
    public int MyMethod(CancellationToken c = default) => 1;
}
            ";
            // Nullability is available in C# 8.0+
            return CS8VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task CS_Diagnostic_WithTrivia()
        {
            string originalCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await /* Prefix1 */ [|MethodDefaultAsync|]() /* Suffix1 */;
        await /* Prefix2 */ [|MethodOverloadAsync|]() /* Suffix2 */;
        await /* Prefix3 */ [|MethodOverloadWithArgumentsAsync|](5 /*ArgumentComment0 */) /* Suffix3 */;
        /* Prefix4 */ [|MethodDefault|]() /* Suffix4 */;
        /* Prefix5 */ [|MethodOverload|]() /* Suffix5 */;
        /* Prefix6 */ [|MethodDefaultWithArguments|](5 /* ArgumentComment1 */) /* Suffix6 */;
        /* Prefix7 */ [|MethodOverloadWithArguments|](5 /* ArgumentComment2 */) /* Suffix7 */;
        /* Prefix8 */ MethodOverloadWithArguments(x: /*ArgumentComment3 */ 5 /* ArgumentComment4 */, ct) /* Suffix8 */;

    }
    Task MethodDefaultAsync(CancellationToken c = default) => Task.CompletedTask;
    Task MethodOverloadAsync() => Task.CompletedTask;
    Task MethodOverloadAsync(CancellationToken c) => Task.CompletedTask;
    Task MethodOverloadWithArgumentsAsync(int x) => Task.CompletedTask;
    Task MethodOverloadWithArgumentsAsync(int x, CancellationToken c) => Task.CompletedTask;
    void MethodDefault(CancellationToken c = default) {}
    void MethodOverload() {}
    void MethodOverload(CancellationToken c) {}
    void MethodDefaultWithArguments(int x, CancellationToken c = default) {}
    void MethodOverloadWithArguments(int x) {}
    void MethodOverloadWithArguments(int x, CancellationToken c) {}
}
            ";
            string fixedCode = @"
using System.Threading;
using System.Threading.Tasks;
class C
{
    async void M(CancellationToken ct)
    {
        await /* Prefix1 */ MethodDefaultAsync(ct) /* Suffix1 */;
        await /* Prefix2 */ MethodOverloadAsync(ct) /* Suffix2 */;
        await /* Prefix3 */ MethodOverloadWithArgumentsAsync(5 /*ArgumentComment0 */, ct) /* Suffix3 */;
        /* Prefix4 */ MethodDefault(ct) /* Suffix4 */;
        /* Prefix5 */ MethodOverload(ct) /* Suffix5 */;
        /* Prefix6 */ MethodDefaultWithArguments(5 /* ArgumentComment1 */, ct) /* Suffix6 */;
        /* Prefix7 */ MethodOverloadWithArguments(5 /* ArgumentComment2 */, ct) /* Suffix7 */;
        /* Prefix8 */ MethodOverloadWithArguments(x: /*ArgumentComment3 */ 5 /* ArgumentComment4 */, ct) /* Suffix8 */;

    }
    Task MethodDefaultAsync(CancellationToken c = default) => Task.CompletedTask;
    Task MethodOverloadAsync() => Task.CompletedTask;
    Task MethodOverloadAsync(CancellationToken c) => Task.CompletedTask;
    Task MethodOverloadWithArgumentsAsync(int x) => Task.CompletedTask;
    Task MethodOverloadWithArgumentsAsync(int x, CancellationToken c) => Task.CompletedTask;
    void MethodDefault(CancellationToken c = default) {}
    void MethodOverload() {}
    void MethodOverload(CancellationToken c) {}
    void MethodDefaultWithArguments(int x, CancellationToken c = default) {}
    void MethodOverloadWithArguments(int x) {}
    void MethodOverloadWithArguments(int x, CancellationToken c) {}
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        [WorkItem(3786, "https://github.com/dotnet/roslyn-analyzers/issues/3786")]
        public Task CS_Diagnostic_MultiNesting_TopMethod()
        {
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    private readonly object lockingObject = new object();
    public void TopMethod(CancellationToken c)
    {
        void LocalMethod()
        {
            bool b = false;
            lock (lockingObject)
            {
                [|TokenMethod|]();
            }
        }
    }
    void TokenMethod(CancellationToken ct = default) {}
}
            ";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    private readonly object lockingObject = new object();
    public void TopMethod(CancellationToken c)
    {
        void LocalMethod()
        {
            bool b = false;
            lock (lockingObject)
            {
                TokenMethod(c);
            }
        }
    }
    void TokenMethod(CancellationToken ct = default) {}
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        [WorkItem(3786, "https://github.com/dotnet/roslyn-analyzers/issues/3786")]
        public Task CS_Diagnostic_MultiNesting_LocalMethod()
        {
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    private readonly object lockingObject = new object();
    public void TopMethod()
    {
        void LocalMethod(CancellationToken c)
        {
            bool b = false;
            lock (lockingObject)
            {
                [|TokenMethod|]();
            }
        }
    }
    void TokenMethod(CancellationToken ct = default) {}
}
            ";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    private readonly object lockingObject = new object();
    public void TopMethod()
    {
        void LocalMethod(CancellationToken c)
        {
            bool b = false;
            lock (lockingObject)
            {
                TokenMethod(c);
            }
        }
    }
    void TokenMethod(CancellationToken ct = default) {}
}
            ";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        [WorkItem(3786, "https://github.com/dotnet/roslyn-analyzers/issues/3786")]
        public Task CS_Diagnostic_LocalMethod_InsideOf_StaticLocalMethodPassingToken()
        {
            // Local static functions are available in C# >= 8.0
            string originalCode = @"
using System;
using System.Threading;
class C
{
    public static void MyMethod(int i, CancellationToken c = default) {}
    public void M(CancellationToken c)
    {
        LocalStaticMethod(c);
        static void LocalStaticMethod(CancellationToken ct)
        {
            LocalMethod();
            void LocalMethod()
            {
                [|MyMethod|](5);
            }
        }
    }
}
            ";
            string fixedCode = @"
using System;
using System.Threading;
class C
{
    public static void MyMethod(int i, CancellationToken c = default) {}
    public void M(CancellationToken c)
    {
        LocalStaticMethod(c);
        static void LocalStaticMethod(CancellationToken ct)
        {
            LocalMethod();
            void LocalMethod()
            {
                MyMethod(5, ct);
            }
        }
    }
}
            ";
            return CS8VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        [WorkItem(4870, "https://github.com/dotnet/roslyn-analyzers/issues/4870")]
        public Task CS_Diagnostic_GenericTypeParamOnInstanceMethod()
        {
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
public class SqlDataReader
{
    public Task<T> GetFieldValueAsync<T>(int i, CancellationToken c = default) => Task.FromResult(default(T));
}
class C
{
    public async Task<Guid> M(SqlDataReader r, CancellationToken c)
    {
        return await [|r.GetFieldValueAsync<Guid>|](0);
    }
}
            ";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
public class SqlDataReader
{
    public Task<T> GetFieldValueAsync<T>(int i, CancellationToken c = default) => Task.FromResult(default(T));
}
class C
{
    public async Task<Guid> M(SqlDataReader r, CancellationToken c)
    {
        return await r.GetFieldValueAsync<Guid>(0, c);
    }
}
            ";
            return CS8VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        [WorkItem(4870, "https://github.com/dotnet/roslyn-analyzers/issues/4870")]
        public Task CS_Diagnostic_GenericTypeParamOnStaticMethod()
        {
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    public static Task<T> GetFieldValueAsync<T>(int i, CancellationToken c = default) => Task.FromResult(default(T));
    public async Task<Guid> M(CancellationToken c)
    {
        return await [|GetFieldValueAsync<Guid>|](0);
    }
}
            ";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    public static Task<T> GetFieldValueAsync<T>(int i, CancellationToken c = default) => Task.FromResult(default(T));
    public async Task<Guid> M(CancellationToken c)
    {
        return await GetFieldValueAsync<Guid>(0, c);
    }
}
            ";
            return CS8VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        [WorkItem(4870, "https://github.com/dotnet/roslyn-analyzers/issues/4870")]
        public Task CS_Diagnostic_NullCoalescedDelegates()
        {
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    delegate Task F(CancellationToken c = default);
    static Task DoF(CancellationToken c = default) => Task.CompletedTask;
    public async Task M(CancellationToken c)
    {
        F f1 = null;
        F f2 = DoF;
        await [|(f1 ?? f2)|]();
    }
}
            ";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    delegate Task F(CancellationToken c = default);
    static Task DoF(CancellationToken c = default) => Task.CompletedTask;
    public async Task M(CancellationToken c)
    {
        F f1 = null;
        F f2 = DoF;
        await [|(f1 ?? f2)|](c);
    }
}
            ";
            return CS8VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        [WorkItem(4870, "https://github.com/dotnet/roslyn-analyzers/issues/4870")]
        public Task CS_Diagnostic_NullCoalescedDelegatesWithInvoke()
        {
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    delegate Task F(CancellationToken c = default);
    static Task DoF(CancellationToken c = default) => Task.CompletedTask;
    public async Task M(CancellationToken c)
    {
        F f1 = null;
        F f2 = DoF;
        await [|(f1 ?? f2).Invoke|]();
    }
}
            ";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
class C
{
    delegate Task F(CancellationToken c = default);
    static Task DoF(CancellationToken c = default) => Task.CompletedTask;
    public async Task M(CancellationToken c)
    {
        F f1 = null;
        F f2 = DoF;
        await (f1 ?? f2).Invoke(c);
    }
}
            ";
            return CS8VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        [WorkItem(4985, "https://github.com/dotnet/roslyn-analyzers/issues/4985")]
        public Task CS_Diagnostic_ReturnTypeIsConvertable()
        {
            // Local static functions are available in C# >= 8.0
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class P
{
    static void M1(string s, CancellationToken cancellationToken)
    {
        long result = [|M2|](s);
    }

    static long M2(string s) { throw new NotImplementedException(); }

    static int M2(string s, CancellationToken cancellationToken) { throw new NotImplementedException(); }
}";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class P
{
    static void M1(string s, CancellationToken cancellationToken)
    {
        long result = M2(s, cancellationToken);
    }

    static long M2(string s) { throw new NotImplementedException(); }

    static int M2(string s, CancellationToken cancellationToken) { throw new NotImplementedException(); }
}";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        [WorkItem(4985, "https://github.com/dotnet/roslyn-analyzers/issues/4985")]
        public Task CS_SpecialCaseTaskLikeReturnTypes()
        {
            // Local static functions are available in C# >= 8.0
            string originalCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class P
{
    static async Task M1Async(string s, CancellationToken cancellationToken)
    {
        int result = await [|M2|](s); // CA2016
    }

    static Task<int> M2(string s) { throw new NotImplementedException(); }

    static ValueTask<int> M2(string s, CancellationToken cancellationToken) { throw new NotImplementedException(); }
}";
            string fixedCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class P
{
    static async Task M1Async(string s, CancellationToken cancellationToken)
    {
        int result = await M2(s, cancellationToken); // CA2016
    }

    static Task<int> M2(string s) { throw new NotImplementedException(); }

    static ValueTask<int> M2(string s, CancellationToken cancellationToken) { throw new NotImplementedException(); }
}";
            return VerifyCS.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        #endregion

        #region No Diagnostic - VB

        [Fact]
        public Task VB_NoDiagnostic_NoParentToken_AsyncNoToken()
        {
            return VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M()
        Await MethodAsync()
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
End Class
            ");
        }

        [Fact]
        public Task VB_NoDiagnostic_NoParentToken_SyncNoToken()
        {
            return VerifyVB.VerifyAnalyzerAsync(@"
Class C
    Private Sub M()
        MyMethod()
    End Sub
    Private Sub MyMethod()
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_NoDiagnostic_NoParentToken_TokenDefault()
        {
            return VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M()
        Await MethodAsync()
    End Sub
    Private Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ");
        }

        [Fact]
        public Task VB_NoDiagnostic_NoToken()
        {
            return VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync()
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
End Class
            ");
        }

        [Fact]
        public Task VB_NoDiagnostic_OverloadArgumentsDontMatch()
        {
            return VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(5, ""Hello, world"")
    End Sub
    Private Function MethodAsync(ByVal i As Integer, ByVal s As String) As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal i As Integer, ByVal ct As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ");
        }

        [Fact]
        public Task VB_NoDiagnostic_Overload_AlreadyPassingToken()
        {
            return VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ");
        }

        [Fact]
        public Task VB_NoDiagnostic_Default_AlreadyPassingToken()
        {
            return VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading
Class C
    Private Sub M(ByVal ct As CancellationToken)
        Method(ct)
    End Sub
    Private Sub Method(ByVal Optional c As CancellationToken = Nothing)
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_NoDiagnostic_PassingTokenFromSource()
        {
            return VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Dim cts As CancellationTokenSource = New CancellationTokenSource()
        Await MethodAsync(cts.Token)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ");
        }

        // There is no default keyword in VB, must use Nothing instead.
        // The following test method covers the two cases for: `default` and `default(CancellationToken)`
        [Fact]
        public Task VB_NoDiagnostic_PassingExplicitNothing()
        {
            return VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(Nothing)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ");
        }

        [Fact]
        public Task VB_NoDiagnostic_PassingExplicitCancellationTokenNone()
        {
            return VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(CancellationToken.None)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ");
        }

        [Fact]
        public Task VB_NoDiagnostic_OverloadTokenNotLastParameter()
        {
            return VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync()
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal x As Integer, ByVal ct As CancellationToken, ByVal s As String) As Task
        Return Task.CompletedTask
    End Function
End Class
            ");
        }

        [Fact]
        public Task VB_NoDiagnostic_OverloadWithMultipleTokens()
        {
            return VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync()
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c1 As CancellationToken, ByVal ct2 As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ");
        }

        [Fact]
        public Task VB_NoDiagnostic_OverloadWithMultipleTokensSeparated()
        {
            return VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync()
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal x As Integer, ByVal c1 As CancellationToken, ByVal s As String, ByVal ct2 As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ");
        }

        [Fact]
        public Task VB_NoDiagnostic_NamedTokenUnordered()
        {
            return VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(s:=""Hello, world"", c:=CancellationToken.None, x:=5)
    End Sub
    Private Function MethodAsync(ByVal x As Integer, ByVal s As String, ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ");
        }

        [Fact]
        public Task VB_NoDiagnostic_Overload_NamedTokenUnordered()
        {
            return VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(s:=""Hello, world"", c:=CancellationToken.None, x:=5)
    End Sub
    Private Function MethodAsync(ByVal x As Integer, ByVal s As String) As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal x As Integer, ByVal s As String, ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ");
        }

        [Fact]
        public Task VB_NoDiagnostic_CancellationTokenSource_ParamsUsed()
        {
            /*
            CancellationTokenSource has 3 different overloads that take CancellationToken arguments.
            We should detect if a ct is passed and not offer a diagnostic, because it's considered one of the `params`.

            public class CancellationTokenSource : IDisposable
            {
                public static CancellationTokenSource CreateLinkedTokenSource(CancellationToken token);
                public static CancellationTokenSource CreateLinkedTokenSource(CancellationToken token1, CancellationToken token2);
                public static CancellationTokenSource CreateLinkedTokenSource(params CancellationToken[] tokens);
            }

            Note: Unlinke C#, in VB the invocation for a static method does not include the type and the dot.
            */
            string originalCode = @"
Imports System.Threading
Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim cts As CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct)
    End Sub
End Class
            ";
            return VB16VerifyAnalyzerAsync(originalCode);
        }

        [Fact]
        public Task VB_NoDiagnostic_ExtensionMethodTakesToken()
        {
            // The extension method is in another class, make sure the object mc is not substituted with the static class name
            string originalCode = @"
Imports System
Imports System.Threading
Imports System.Runtime.CompilerServices
Class C
    Public Sub M(ByVal ct As CancellationToken)
        Dim mc As [MyClass] = New [MyClass]()
        mc.MyMethod()
    End Sub
End Class
Public Class [MyClass]
    Public Sub MyMethod()
    End Sub
End Class
Module Extensions
    <Extension()>
    Sub MyMethod(ByVal mc As [MyClass], ByVal c As CancellationToken)
    End Sub
End Module
            ";
            return VB16VerifyAnalyzerAsync(originalCode);
        }

        [Fact]
        [WorkItem(3786, "https://github.com/dotnet/roslyn-analyzers/issues/3786")]
        public Task VB_NoDiagnostic_LambdaAndExtensionMethod_NoTokenInLambda()
        {
            // Only for local methods will we look for the ct in the top-most ancestor
            // For anonymous methods we will only look in the immediate ancestor
            string originalCode = @"
Imports System
Imports System.Threading
Imports System.Runtime.CompilerServices

Module Extensions
    <Extension()>
    Sub Extension(ByVal b As Boolean, ByVal action As Action(Of Integer))
    End Sub

    <Extension()>
    Sub MyMethod(ByVal i As Integer, ByVal Optional c As CancellationToken = Nothing)
    End Sub
End Module

Class C
    Public Sub M(ByVal ct As CancellationToken)
        Dim b As Boolean = False
        b.Extension(Sub(j)
                        j.MyMethod()
                    End Sub)
    End Sub
End Class
            ";
            return VerifyVB.VerifyAnalyzerAsync(originalCode);
        }

        [Fact]
        [WorkItem(3786, "https://github.com/dotnet/roslyn-analyzers/issues/3786")]
        public Task VB_NoDiagnostic_AnonymousDelegateAndExtensionMethod_NoTokenInAnonymousDelegate()
        {
            // Only for local methods will we look for the ct in the top-most ancestor
            // For anonymous methods we will only look in the immediate ancestor
            return VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Threading
Imports System.Runtime.CompilerServices
Module Extensions
    Public Delegate Sub MyDelegate(ByVal i As Integer)
    <Extension()>
    Sub Extension(ByVal b As Boolean, ByVal d As MyDelegate)
    End Sub
    <Extension()>
    Sub MyMethod(ByVal i As Integer, ByVal Optional c As CancellationToken = Nothing)
    End Sub
End Module
Class C
    Public Sub M(ByVal ct As CancellationToken)
        Dim b As Boolean = False
        b.Extension(Sub(ByVal j As Integer)
                        j.MyMethod()
                    End Sub)
    End Sub
End Class
            ");
        }

        [Fact]
        [WorkItem(4985, "https://github.com/dotnet/roslyn-analyzers/issues/4985")]
        public Task VB_NoDiagnostic_ReturnTypesDiffer()
        {
            return VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Threading
Imports System.Threading.Tasks

Module Program
    Sub M1(s As String, cancellationToken As CancellationToken)
        Dim result = M2(s)
    End Sub

    Function M2(s As String) As Task
        Throw New NotImplementedException
    End Function

    Function M2(s As String, cancellationToken As CancellationToken) As Integer
        Throw New NotImplementedException
    End Function
End Module
");
        }

        #endregion

        #region Diagnostics with no fix = VB

        [Fact]
        public Task VB_AnalyzerOnlyDiagnostic_OverloadWithNamedParametersUnordered()
        {
            // This is a special case that will get a diagnostic but will not get a fix
            // because the fixer does not currently have a way to know the overload's ct parameter name
            // VB arguments get reordered in their official parameter order, so we could add the ct argument at the end
            // and VB would compile successfully (CA8323 would not be thrown), but that would require separate VB
            // handling in the fixer, so instead, the C# and VB behavior will remain the same
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Function M(ByVal ct As CancellationToken) As Task
        Return [|MethodAsync|](z:=""Hello world"", x:=5, y:=true)
    End Function
    Private Function MethodAsync(ByVal x As Integer, ByVal Optional y As Boolean = false, ByVal Optional z As String = """") As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal x As Integer, ByVal Optional y As Boolean = false, ByVal Optional z As String = """", ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyAnalyzerAsync(originalCode);
        }

        [Fact]
        public Task VB_AnalyzerOnlyDiagnostic_CancellationTokenSource_ParamsEmpty()
        {
            /*
            CancellationTokenSource has 3 different overloads that take CancellationToken arguments.
            When no ct is passed, because the overload that takes one instance is not setting a default value, then the analyzer considers it the `params`.
            No fix provided.

            public class CancellationTokenSource : IDisposable
            {
                public static CancellationTokenSource CreateLinkedTokenSource(CancellationToken token);
                public static CancellationTokenSource CreateLinkedTokenSource(CancellationToken token1, CancellationToken token2);
                public static CancellationTokenSource CreateLinkedTokenSource(params CancellationToken[] tokens);
            }

            Note: Unlinke C#, in VB the invocation for a static method does not include the type and the dot.
            */
            string originalCode = @"
Imports System.Threading
Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim cts As CancellationTokenSource = CancellationTokenSource.[|CreateLinkedTokenSource|]()
    End Sub
End Class
            ";
            return VB16VerifyAnalyzerAsync(originalCode);
        }

        #endregion

        #region Diagnostics with fix = VB

        [Fact]
        public Task VB_Diagnostic_Class_TokenDefault()
        {
            string originalCode = @"
Imports System.Threading
Class C
    Private Sub M(ByVal ct As CancellationToken)
        [|MyMethod|]()
    End Sub
    Private Function MyMethod(ByVal Optional c As CancellationToken = Nothing) As Integer
        Return 1
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Class C
    Private Sub M(ByVal ct As CancellationToken)
        MyMethod(ct)
    End Sub
    Private Function MyMethod(ByVal Optional c As CancellationToken = Nothing) As Integer
        Return 1
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_Class_TokenDefault_WithConfigureAwait()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await [|MethodAsync|]().ConfigureAwait(False)
    End Sub
    Private Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(ct).ConfigureAwait(False)
    End Sub
    Private Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_NoAwait()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Sub M(ByVal ct As CancellationToken)
        [|MethodAsync|]()
    End Sub
    Private Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Sub M(ByVal ct As CancellationToken)
        MethodAsync(ct)
    End Sub
    Private Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_SaveTask()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks

Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim t As Task = [|MethodAsync|]()
    End Sub
    Private Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks

Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim t As Task = MethodAsync(ct)
    End Sub
    Private Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_ClassStaticMethod_TokenDefault()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await [|MethodAsync|]()
    End Sub
    Private Shared Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(ct)
    End Sub
    Private Shared Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_ClassStaticMethod_TokenDefault_WithConfigureAwait()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await [|MethodAsync|]().ConfigureAwait(False)
    End Sub
    Private Shared Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(ct).ConfigureAwait(False)
    End Sub
    Private Shared Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_OtherClass_TokenDefault()
        {
            string originalCode = @"
Imports System.Threading
Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim o As O = New O()
        o.[|MyMethod|]()
    End Sub
End Class
Class O
    Public Function MyMethod(ByVal Optional c As CancellationToken = Nothing) As Integer
        Return 1
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim o As O = New O()
        o.MyMethod(ct)
    End Sub
End Class
Class O
    Public Function MyMethod(ByVal Optional c As CancellationToken = Nothing) As Integer
        Return 1
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_OtherClass_TokenDefault_WithConfigureAwait()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Dim o As O = New O()
        Await o.[|MethodAsync|]()
    End Sub
End Class
Class O
    Public Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Public Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Dim o As O = New O()
        Await o.MethodAsync(ct)
    End Sub
End Class
Class O
    Public Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Public Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_OtherClassStaticMethod_TokenDefault()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await O.[|MethodAsync|]()
    End Sub
End Class
Class O
    Public Shared Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await O.MethodAsync(ct)
    End Sub
End Class
Class O
    Public Shared Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_OtherClassStaticMethod_TokenDefault_WithConfigureAwait()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Dim o As O = New O()
        Await o.[|MethodAsync|]()
    End Sub
End Class
Class O
    Public Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Public Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Dim o As O = New O()
        Await o.MethodAsync(ct)
    End Sub
End Class
Class O
    Public Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Public Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_Struct_TokenDefault()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Structure S
    Private Async Sub M(ByVal ct As CancellationToken)
        Await [|MethodAsync|]()
    End Sub
    Private Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Structure
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Structure S
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(ct)
    End Sub
    Private Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Structure
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_Struct_TokenDefault_WithConfigureAwait()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Structure S
    Private Async Sub M(ByVal ct As CancellationToken)
        Await [|MethodAsync|]().ConfigureAwait(False)
    End Sub
    Private Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Structure
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Structure S
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(ct).ConfigureAwait(False)
    End Sub
    Private Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Structure
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_OverloadToken()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await [|MethodAsync|]()
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_OverloadToken_WithConfigureAwait()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await [|MethodAsync|]()
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_OverloadTokenDefault()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await [|MethodAsync|]()
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_OverloadTokenDefault_WithConfigureAwait()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await [|MethodAsync|]().ConfigureAwait(False)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(ct).ConfigureAwait(False)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_OverloadsArgumentsMatch()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await [|MethodAsync|](5, ""Hello, world"")
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal x As Integer, ByVal s As String) As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal x As Integer, ByVal s As String, ByVal ct As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(5, ""Hello, world"", ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal x As Integer, ByVal s As String) As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal x As Integer, ByVal s As String, ByVal ct As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_OverloadsArgumentsMatch_WithConfigureAwait()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await [|MethodAsync|](5, ""Hello, world"").ConfigureAwait(True)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal x As Integer, ByVal s As String) As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal x As Integer, ByVal s As String, ByVal ct As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(5, ""Hello, world"", ct).ConfigureAwait(True)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal x As Integer, ByVal s As String) As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal x As Integer, ByVal s As String, ByVal ct As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_ActionDelegateAwait()
        {
            string originalCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim a As Action(Of CancellationToken) = Async Sub(ByVal token As CancellationToken) Await [|MethodAsync|]()
        a(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim a As Action(Of CancellationToken) = Async Sub(ByVal token As CancellationToken) Await MethodAsync(token)
        a(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_ActionDelegateNoAwait()
        {
            string originalCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim a As Action(Of CancellationToken) = Sub(ByVal c As CancellationToken) [|MethodAsync|]()
        a(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim a As Action(Of CancellationToken) = Sub(ByVal c As CancellationToken) MethodAsync(c)
        a(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_ActionDelegateAwait_WithConfigureAwait()
        {
            string originalCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim a As Action(Of CancellationToken) = Async Sub(ByVal token As CancellationToken) Await [|MethodAsync|]().ConfigureAwait(False)
        a(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim a As Action(Of CancellationToken) = Async Sub(ByVal token As CancellationToken) Await MethodAsync(token).ConfigureAwait(False)
        a(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_FuncDelegateAwait()
        {
            string originalCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim f As Func(Of CancellationToken, Task(Of Boolean)) = Async Function(ByVal token As CancellationToken)
                                                                    Await [|MethodAsync|]()
                                                                    Return True
                                                                End Function
        f(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim f As Func(Of CancellationToken, Task(Of Boolean)) = Async Function(ByVal token As CancellationToken)
                                                                    Await MethodAsync(token)
                                                                    Return True
                                                                End Function
        f(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_FuncDelegateNoAwait()
        {
            string originalCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim f As Func(Of CancellationToken, Boolean) = Function(ByVal token As CancellationToken)
                                                           [|MethodAsync|]()
                                                           Return True
                                                        End Function
        f(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim f As Func(Of CancellationToken, Boolean) = Function(ByVal token As CancellationToken)
                                                           MethodAsync(token)
                                                           Return True
                                                        End Function
        f(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_FuncDelegateAwaitOutside()
        {
            string originalCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Dim f As Func(Of CancellationToken, Task) = Function(ByVal c As CancellationToken) [|MethodAsync|]()
        Await f(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Dim f As Func(Of CancellationToken, Task) = Function(ByVal c As CancellationToken) MethodAsync(c)
        Await f(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_FuncDelegateAwait_WithConfigureAwait()
        {
            string originalCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim f As Func(Of CancellationToken, Task(Of Boolean)) = Async Function(ByVal token As CancellationToken)
                                                                    Await [|MethodAsync|]().ConfigureAwait(True)
                                                                    Return True
                                                                End Function
        f(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Sub M(ByVal ct As CancellationToken)
        Dim f As Func(Of CancellationToken, Task(Of Boolean)) = Async Function(ByVal token As CancellationToken)
                                                                    Await MethodAsync(token).ConfigureAwait(True)
                                                                    Return True
                                                                End Function
        f(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        // Nested functions not available in VB:
        // VB_Diagnostic_NestedFunctionAwait
        // VB_Diagnostic_NestedFunctionNoAwait
        // VB_Diagnostic_NestedFunctionAwaitOutside
        // VB_Diagnostic_NestedFunctionAwait_WithConfigureAwait

        [Fact]
        public Task VB_Diagnostic_AliasTokenInOverload()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Imports TokenAlias = System.Threading.CancellationToken
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await [|MethodAsync|]()
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As TokenAlias) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Imports TokenAlias = System.Threading.CancellationToken
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As TokenAlias) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_Default_AliasTokenInMethodParameter()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Imports TokenAlias = System.Threading.CancellationToken
Class C
    Private Async Sub M(ByVal ct As TokenAlias)
        Await [|MethodAsync|]()
    End Sub
    Private Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Imports TokenAlias = System.Threading.CancellationToken
Class C
    Private Async Sub M(ByVal ct As TokenAlias)
        Await MethodAsync(ct)
    End Sub
    Private Function MethodAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_Overload_AliasTokenInMethodParameter()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Imports TokenAlias = System.Threading.CancellationToken
Class C
    Private Async Sub M(ByVal ct As TokenAlias)
        Await [|MethodAsync|]()
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Imports TokenAlias = System.Threading.CancellationToken
Class C
    Private Async Sub M(ByVal ct As TokenAlias)
        Await MethodAsync(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_Default_AliasTokenInDefaultAndMethodParameter()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Imports TokenAlias = System.Threading.CancellationToken
Class C
    Private Async Sub M(ByVal ct As TokenAlias)
        Await [|MethodAsync|]()
    End Sub
    Private Function MethodAsync(ByVal Optional c As TokenAlias = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Imports TokenAlias = System.Threading.CancellationToken
Class C
    Private Async Sub M(ByVal ct As TokenAlias)
        Await MethodAsync(ct)
    End Sub
    Private Function MethodAsync(ByVal Optional c As TokenAlias = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_Overload_AliasTokenInOverloadAndMethodParameter()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Imports TokenAlias = System.Threading.CancellationToken
Class C
    Private Async Sub M(ByVal ct As TokenAlias)
        Await [|MethodAsync|]()
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Imports TokenAlias = System.Threading.CancellationToken
Class C
    Private Async Sub M(ByVal ct As TokenAlias)
        Await MethodAsync(ct)
    End Sub
    Private Function MethodAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_Default_WithAllDefaultParametersImplicit()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Function M(ByVal ct As CancellationToken) As Task
        Return [|MethodAsync|]()
    End Function
    Private Function MethodAsync(ByVal Optional x As Integer = 0, ByVal Optional y As Boolean = False, ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Function M(ByVal ct As CancellationToken) As Task
        Return MethodAsync(c:=ct)
    End Function
    Private Function MethodAsync(ByVal Optional x As Integer = 0, ByVal Optional y As Boolean = False, ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_Default_WithSomeDefaultParameters()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await [|MethodAsync|](5)
    End Sub
    Private Function MethodAsync(ByVal x As Integer, ByVal Optional y As Boolean = false, ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(5, c:=ct)
    End Sub
    Private Function MethodAsync(ByVal x As Integer, ByVal Optional y As Boolean = false, ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_Default_WithNamedParameters()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await [|MethodAsync|](x:=5)
    End Sub
    Private Function MethodAsync(ByVal x As Integer, ByVal Optional y As Boolean = false, ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(x:=5, c:=ct)
    End Sub
    Private Function MethodAsync(ByVal x As Integer, ByVal Optional y As Boolean = false, ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_Default_WithAncestorAliasAndNamedParameters()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Imports TokenAlias = System.Threading.CancellationToken
Class C
    Private Async Sub M(ByVal ct As TokenAlias)
        Await [|MethodAsync|](x:=5)
    End Sub
    Private Function MethodAsync(ByVal x As Integer, ByVal Optional y As Boolean = false, ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Imports TokenAlias = System.Threading.CancellationToken
Class C
    Private Async Sub M(ByVal ct As TokenAlias)
        Await MethodAsync(x:=5, c:=ct)
    End Sub
    Private Function MethodAsync(ByVal x As Integer, ByVal Optional y As Boolean = false, ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_Default_WithMethodArgumentAliasAndNamedParameters()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Imports TokenAlias = System.Threading.CancellationToken
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await [|MethodAsync|](x:=5)
    End Sub
    Private Function MethodAsync(ByVal x As Integer, ByVal Optional y As Boolean = false, ByVal Optional c As TokenAlias = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Imports TokenAlias = System.Threading.CancellationToken
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodAsync(x:=5, c:=ct)
    End Sub
    Private Function MethodAsync(ByVal x As Integer, ByVal Optional y As Boolean = false, ByVal Optional c As TokenAlias = Nothing) As Task
        Return Task.CompletedTask
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_Default_WithNamedParametersUnordered()
        {
            string originalCode = @"
Imports System.Threading
Class C
    Private Function M(ByVal ct As CancellationToken) As Integer
        Return [|MyMethod|](z:=""Hello world"", x:=5, y:=true)
    End Function
    Private Function MyMethod(ByVal x As Integer, ByVal Optional y As Boolean = false, ByVal Optional z As String = """", ByVal Optional c As CancellationToken = Nothing) As Integer
        Return 1
    End Function
End Class
            ";
            // Notice the order is preserved and the missing implicit parameters are appended as they are found
            string fixedCode = @"
Imports System.Threading
Class C
    Private Function M(ByVal ct As CancellationToken) As Integer
        Return MyMethod(z:=""Hello world"", x:=5, y:=true, c:=ct)
    End Function
    Private Function MyMethod(ByVal x As Integer, ByVal Optional y As Boolean = false, ByVal Optional z As String = """", ByVal Optional c As CancellationToken = Nothing) As Integer
        Return 1
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_WithLock()
        {
            string originalCode = @"
Imports System.Threading
Class C
    Private ReadOnly lockingObject As Object = New Object()
    Private Function M(ByVal ct As CancellationToken) As Integer
        Dim x As Integer
        SyncLock lockingObject
            x = [|MyMethod|](5)
        End SyncLock
        Return x
    End Function
    Private Function MyMethod(ByVal x As Integer, ByVal Optional c As CancellationToken = Nothing) As Integer
        Return 1
    End Function
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Class C
    Private ReadOnly lockingObject As Object = New Object()
    Private Function M(ByVal ct As CancellationToken) As Integer
        Dim x As Integer
        SyncLock lockingObject
            x = MyMethod(5, ct)
        End SyncLock
        Return x
    End Function
    Private Function MyMethod(ByVal x As Integer, ByVal Optional c As CancellationToken = Nothing) As Integer
        Return 1
    End Function
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_DereferencePossibleNullReference()
        {
            string originalCode = @"
Imports System.Threading
Class C
    Private Function PossiblyNull() As O?
        Return Nothing
    End Function
    Private Sub M(ByVal ct As CancellationToken)
        Dim o As O? = PossiblyNull()
        o?.[|MyMethod|]()
    End Sub
End Class
Structure O
    Public Function MyMethod(ByVal Optional c As CancellationToken = Nothing) As Integer
        Return 1
    End Function
End Structure
            ";
            string fixedCode = @"
Imports System.Threading
Class C
    Private Function PossiblyNull() As O?
        Return Nothing
    End Function
    Private Sub M(ByVal ct As CancellationToken)
        Dim o As O? = PossiblyNull()
        o?.MyMethod(ct)
    End Sub
End Class
Structure O
    Public Function MyMethod(ByVal Optional c As CancellationToken = Nothing) As Integer
        Return 1
    End Function
End Structure
            ";
            // Nullability is available in C# 8.0+
            return VB16VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        public Task VB_Diagnostic_WithTrivia()
        {
            string originalCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await [|MethodDefaultAsync|]() ' InvocationComment1
        Await [|MethodOverloadAsync|]() ' InvocationComment2
        Await [|MethodOverloadWithArgumentsAsync|](5) ' InvocationComment3
        [|MethodDefault|]() ' InvocationComment4
        [|MethodOverload|]() ' InvocationComment5
        [|MethodDefaultWithArguments|](5) ' InvocationComment6
        [|MethodOverloadWithArguments|](5) ' InvocationComment7
    End Sub
    Private Function MethodDefaultAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodOverloadAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodOverloadAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodOverloadWithArgumentsAsync(ByVal x As Integer) As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodOverloadWithArgumentsAsync(ByVal x As Integer, ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
    Private Sub MethodDefault(ByVal Optional c As CancellationToken = Nothing)
    End Sub
    Private Sub MethodOverload()
    End Sub
    Private Sub MethodOverload(ByVal c As CancellationToken)
    End Sub
    Private Sub MethodDefaultWithArguments(ByVal x As Integer, ByVal Optional c As CancellationToken = Nothing)
    End Sub
    Private Sub MethodOverloadWithArguments(ByVal x As Integer)
    End Sub
    Private Sub MethodOverloadWithArguments(ByVal x As Integer, ByVal c As CancellationToken)
    End Sub
End Class
            ";
            string fixedCode = @"
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Private Async Sub M(ByVal ct As CancellationToken)
        Await MethodDefaultAsync(ct) ' InvocationComment1
        Await MethodOverloadAsync(ct) ' InvocationComment2
        Await MethodOverloadWithArgumentsAsync(5, ct) ' InvocationComment3
        MethodDefault(ct) ' InvocationComment4
        MethodOverload(ct) ' InvocationComment5
        MethodDefaultWithArguments(5, ct) ' InvocationComment6
        MethodOverloadWithArguments(5, ct) ' InvocationComment7
    End Sub
    Private Function MethodDefaultAsync(ByVal Optional c As CancellationToken = Nothing) As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodOverloadAsync() As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodOverloadAsync(ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodOverloadWithArgumentsAsync(ByVal x As Integer) As Task
        Return Task.CompletedTask
    End Function
    Private Function MethodOverloadWithArgumentsAsync(ByVal x As Integer, ByVal c As CancellationToken) As Task
        Return Task.CompletedTask
    End Function
    Private Sub MethodDefault(ByVal Optional c As CancellationToken = Nothing)
    End Sub
    Private Sub MethodOverload()
    End Sub
    Private Sub MethodOverload(ByVal c As CancellationToken)
    End Sub
    Private Sub MethodDefaultWithArguments(ByVal x As Integer, ByVal Optional c As CancellationToken = Nothing)
    End Sub
    Private Sub MethodOverloadWithArguments(ByVal x As Integer)
    End Sub
    Private Sub MethodOverloadWithArguments(ByVal x As Integer, ByVal c As CancellationToken)
    End Sub
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        [WorkItem(3786, "https://github.com/dotnet/roslyn-analyzers/issues/3786")]
        public Task VB_Diagnostic_MultiNesting_TopMethod()
        {
            // Local methods do not exist in VB, it's the only difference with the CS mirror test
            string originalCode = $@"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Runtime.CompilerServices
Class C
    Private ReadOnly lockingObject As Object = New Object()
    Public Sub TopMethod(c As CancellationToken)
        Dim b As Boolean = False
        SyncLock lockingObject
            [|TokenMethod|]()
        End SyncLock
    End Sub
    Private Sub TokenMethod(ByVal Optional ct As CancellationToken = Nothing)
    End Sub
End Class
            ";
            string fixedCode = $@"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Runtime.CompilerServices
Class C
    Private ReadOnly lockingObject As Object = New Object()
    Public Sub TopMethod(c As CancellationToken)
        Dim b As Boolean = False
        SyncLock lockingObject
            TokenMethod(c)
        End SyncLock
    End Sub
    Private Sub TokenMethod(ByVal Optional ct As CancellationToken = Nothing)
    End Sub
End Class
            ";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        [WorkItem(4870, "https://github.com/dotnet/roslyn-analyzers/issues/4870")]
        public Task VB_Diagnostic_GenericTypeParamOnInstanceMethod()
        {
            string originalCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Public Class SqlDataReader
    Public Function GetFieldValueAsync(Of T)(ByVal i As Integer, ByVal Optional c As CancellationToken = Nothing) As Task(Of T)
        Return Task.CompletedTask
    End Function
End Class
Class C
    Public Async Function M(ByVal r As SqlDataReader, ByVal c As CancellationToken) As Task(Of Guid)
        Return Await r.[|GetFieldValueAsync(Of Guid)|](0)
    End Function
End Class
";
            string fixedCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Public Class SqlDataReader
    Public Function GetFieldValueAsync(Of T)(ByVal i As Integer, ByVal Optional c As CancellationToken = Nothing) As Task(Of T)
        Return Task.CompletedTask
    End Function
End Class
Class C
    Public Async Function M(ByVal r As SqlDataReader, ByVal c As CancellationToken) As Task(Of Guid)
        Return Await r.GetFieldValueAsync(Of Guid)(0, c)
    End Function
End Class
";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        [WorkItem(4870, "https://github.com/dotnet/roslyn-analyzers/issues/4870")]
        public Task VB_Diagnostic_GenericTypeParamOnStaticMethod()
        {
            string originalCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Public Shared Function GetFieldValueAsync(Of T)(ByVal i As Integer, Optional ByVal c As CancellationToken = Nothing) As Task(Of T)
        Return Task.CompletedTask
    End Function
    Public Async Function M(ByVal c As CancellationToken) As Task(Of Guid)
        Return Await [|GetFieldValueAsync(Of Guid)|](0)
    End Function
End Class
";
            string fixedCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Public Shared Function GetFieldValueAsync(Of T)(ByVal i As Integer, Optional ByVal c As CancellationToken = Nothing) As Task(Of T)
        Return Task.CompletedTask
    End Function
    Public Async Function M(ByVal c As CancellationToken) As Task(Of Guid)
        Return Await GetFieldValueAsync(Of Guid)(0, c)
    End Function
End Class
";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        [WorkItem(4985, "https://github.com/dotnet/roslyn-analyzers/issues/4985")]
        public Task VB_Diagnostic_ReturnTypeIsConvertable()
        {
            // Local static functions are available in C# >= 8.0
            string originalCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks

Module Program
    Sub M1(s As String, cancellationToken As CancellationToken)
        Dim result As Long = [|M2|](s)
    End Sub

    Function M2(s As String) As Long
        Throw New NotImplementedException
    End Function

    Function M2(s As String, cancellationToken As CancellationToken) As Integer
        Throw New NotImplementedException
    End Function
End Module
";
            string fixedCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks

Module Program
    Sub M1(s As String, cancellationToken As CancellationToken)
        Dim result As Long = M2(s, cancellationToken)
    End Sub

    Function M2(s As String) As Long
        Throw New NotImplementedException
    End Function

    Function M2(s As String, cancellationToken As CancellationToken) As Integer
        Throw New NotImplementedException
    End Function
End Module
";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        [Fact]
        [WorkItem(4985, "https://github.com/dotnet/roslyn-analyzers/issues/4985")]
        public Task VB_SpecialCaseTaskLikeReturnTypes()
        {
            // Local static functions are available in C# >= 8.0
            string originalCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks

Module Program
    Async Function M1Async(s As String, cancellationToken As CancellationToken) As Task
        Dim result As Integer = Await [|M2|](s)
    End Function

    Function M2(s As String) As Task(Of Integer)
        Throw New NotImplementedException
    End Function

    Function M2(s As String, cancellationToken As CancellationToken) As ValueTask(Of Integer)
        Throw New NotImplementedException
    End Function
End Module
";
            string fixedCode = @"
Imports System
Imports System.Threading
Imports System.Threading.Tasks

Module Program
    Async Function M1Async(s As String, cancellationToken As CancellationToken) As Task
        Dim result As Integer = Await M2(s, cancellationToken)
    End Function

    Function M2(s As String) As Task(Of Integer)
        Throw New NotImplementedException
    End Function

    Function M2(s As String, cancellationToken As CancellationToken) As ValueTask(Of Integer)
        Throw New NotImplementedException
    End Function
End Module
";
            return VerifyVB.VerifyCodeFixAsync(originalCode, fixedCode);
        }

        #endregion

        #region Helpers

        private static async Task CS8VerifyCodeFixAsync(string originalCode, string fixedCode)
        {
            var test = new VerifyCS.Test
            {
                TestCode = originalCode,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                FixedCode = fixedCode,
            };

            test.ExpectedDiagnostics.AddRange(DiagnosticResult.EmptyDiagnosticResults);
            await test.RunAsync();
        }

        private static async Task CS8VerifyAnalyzerAsync(string originalCode)
        {
            var test = new VerifyCS.Test
            {
                TestCode = originalCode,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };

            test.ExpectedDiagnostics.AddRange(DiagnosticResult.EmptyDiagnosticResults);
            await test.RunAsync();
        }

        private static async Task VB16VerifyCodeFixAsync(string originalCode, string fixedCode)
        {
            var test = new VerifyVB.Test
            {
                TestCode = originalCode,
                LanguageVersion = CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                FixedCode = fixedCode
            };

            test.ExpectedDiagnostics.AddRange(DiagnosticResult.EmptyDiagnosticResults);
            await test.RunAsync();
        }

        private static async Task VB16VerifyAnalyzerAsync(string originalCode)
        {
            var test = new VerifyVB.Test
            {
                TestCode = originalCode,
                LanguageVersion = CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };

            test.ExpectedDiagnostics.AddRange(DiagnosticResult.EmptyDiagnosticResults);
            await test.RunAsync();
        }

        #endregion
    }
}