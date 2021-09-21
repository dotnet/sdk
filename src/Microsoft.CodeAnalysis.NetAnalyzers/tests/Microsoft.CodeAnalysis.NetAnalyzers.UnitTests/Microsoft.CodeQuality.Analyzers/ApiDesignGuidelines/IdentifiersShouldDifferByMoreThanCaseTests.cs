// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.IdentifiersShouldDifferByMoreThanCaseAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class IdentifiersShouldDifferByMoreThanCaseTests
    {
        #region Namespace Level

        [Fact]
        public async Task TestGlobalNamespaceNamesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace N
{
    public class C { }
}
namespace n
{
    public class C { }
}
",
                GetGlobalCA1708CSharpResult(Namespace, GetSymbolDisplayString("n", "N")));
        }

        [Fact]
        public async Task TestNestedNamespaceNamesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace N
{
    class C { }
    namespace n 
    {
        public class C { }
    }
    namespace n { }
}

namespace n
{
    public class C { }
    namespace n
    {
        public class C { }
    }
}
",
                GetGlobalCA1708CSharpResult(Namespace, GetSymbolDisplayString("n", "N")));
        }

        [Fact]
        public async Task TestGlobalTypeNamesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Ni
{
}
public struct ni
{
}
public interface nI
{
}
",
                GetGlobalCA1708CSharpResult(Type, GetSymbolDisplayString("nI", "ni", "Ni")));
        }

        [Fact]
        public async Task TestGenericClassesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C<T>
{
}
public class c<S>
{
}
public class c
{
}
public class C<T,X>
{
}
",
                GetGlobalCA1708CSharpResult(Type, GetSymbolDisplayString("c<S>", "C<T>")));
        }

        [Fact]
        public async Task TestPartialTypesAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
namespace N
{
    public partial class C
    {
        public int x;
    }
    public partial class C
    {
        public int X;
    }
    public partial class F
    {
        public int x;
    }
}
",
                        @"
namespace N
{
    public class c
    {
    }
    public partial class F
    {
        public int X;
    }
}"
                    },
                    ExpectedDiagnostics =
                    {
                        GetGlobalCA1708CSharpResult(Type, GetSymbolDisplayString("N.C", "N.c")),
                        GetCA1708CSharpResultAt(Member, GetSymbolDisplayString("N.C.x", "N.C.X"), ("/0/Test0.cs", 4, 26), ("/0/Test0.cs", 8, 26)),
                        GetCA1708CSharpResultAt(Member, GetSymbolDisplayString("N.F.x", "N.F.X"), ("/0/Test0.cs", 12, 26), ("/0/Test1.cs", 7, 26)),
                    }
                }
            }.RunAsync();
        }

        #endregion

        #region Type Level

        [Fact]
        public async Task TestNestedTypeNamesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace NI
{
    public class Ni
    {    
        public struct Nd { }
        public delegate void nd();
        public class C
        {
            public struct nD { }
            public class nd
            {
                public class CI { }
                public struct ci
                {
                    public int x;
                    public void X() { }
                }
                public interface Ci 
                {
                    void method();
                    void Method();
                }
           }
        }
    }
   
    class NI
    {
        public class N 
        {
        }
        public class n { }
    }
}
",
            GetGlobalCA1708CSharpResult(Type, GetSymbolDisplayString("NI.Ni", "NI.NI")),
            GetCA1708CSharpResultAt(4, 18, Member, GetSymbolDisplayString("NI.Ni.Nd", "NI.Ni.nd")),
            GetCA1708CSharpResultAt(8, 22, Member, GetSymbolDisplayString("NI.Ni.C.nD", "NI.Ni.C.nd")),
            GetCA1708CSharpResultAt(11, 26, Member, GetSymbolDisplayString("NI.Ni.C.nd.CI", "NI.Ni.C.nd.ci", "NI.Ni.C.nd.Ci")),
            GetCA1708CSharpResultAt(14, 31, Member, GetSymbolDisplayString("NI.Ni.C.nd.ci.x", "NI.Ni.C.nd.ci.X()")),
            GetCA1708CSharpResultAt(19, 34, Member, GetSymbolDisplayString("NI.Ni.C.nd.Ci.method()", "NI.Ni.C.nd.Ci.Method()")));
        }

        [Fact]
        public async Task TestNestedTypeNamesWithScopeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
namespace NI
{
    public class Ni
    {    
        public struct Nd { }
        public delegate void nd();
        public class C
        {
            public struct nD { }
            public class nd
            {
                public class CI { }
                public struct ci
                {
                    public int x;
                    public void X() { }
                }
                public interface Ci 
                {
                    void method();
                    void Method();
                }
           }
        }
    }
   
    class NI
    {
        public class N 
        {
        }
        public class n { }
    }
}
",
            GetGlobalCA1708CSharpResult(Type, GetSymbolDisplayString("NI.Ni", "NI.NI")),
            GetCA1708CSharpResultAt(5, 18, Member, GetSymbolDisplayString("NI.Ni.Nd", "NI.Ni.nd")),
            GetCA1708CSharpResultAt(9, 22, Member, GetSymbolDisplayString("NI.Ni.C.nD", "NI.Ni.C.nd")),
            GetCA1708CSharpResultAt(12, 26, Member, GetSymbolDisplayString("NI.Ni.C.nd.CI", "NI.Ni.C.nd.Ci", "NI.Ni.C.nd.ci")),
            GetCA1708CSharpResultAt(15, 31, Member, GetSymbolDisplayString("NI.Ni.C.nd.ci.X()", "NI.Ni.C.nd.ci.x")),
            GetCA1708CSharpResultAt(20, 34, Member, GetSymbolDisplayString("NI.Ni.C.nd.Ci.Method()", "NI.Ni.C.nd.Ci.method()")));
        }

        [Fact]
        public async Task TestMethodOverloadsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace NI
{
    public class C
    {
        public void method() { }
        public void method(int x) { }
        public void method<T>(T x) { }
    }
}
");
        }

        [Fact]
        public async Task TestGenericMethodsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace NI
{
    public class C
    {
        public void method() { }
        public void methoD(int x) { }
        public void mEthod(int x) { }
        public void METHOD<T>(T x) { }
        public void mEthod<T, X>(T x, X y) { }
    }
}
",
            GetCA1708CSharpResultAt(4, 18, Member, GetSymbolDisplayString("NI.C.method()", "NI.C.methoD(int)", "NI.C.mEthod(int)", "NI.C.METHOD<T>(T)")));
        }

        [Fact]
        public async Task TestMembersAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace NI
{
    public class CASE1
    {
        public int Case1;
        void CAse1() { caSE1(); }
        public void CAse1(int x) { }
        public void CaSe1<T>(T x) { }
        public delegate void CASe1();
        public interface CasE1 { }
        public event CASe1 caSE1;
        public int CAsE1
        {
            get { return Case1; }
            set { Case1 = value; }
        }
        public CASE1() { }
        public int this[int x]
        {
            get { return x; }
            set { }    
        }
        ~CASE1() { }
        static CASE1() { }
        public static int operator +(CASE1 y, int x) { return 1; }
    }
}
",
            GetCA1708CSharpResultAt(4, 18, Member, GetSymbolDisplayStringNoSorting("NI.CASE1.CASe1", "NI.CASE1.CAsE1", "NI.CASE1.CAse1(int)", "NI.CASE1.CaSe1<T>(T)", "NI.CASE1.CasE1", "NI.CASE1.Case1", "NI.CASE1.caSE1")));
        }

        [Fact]
        public async Task TestCultureSpecificNamesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int γ;
    public int Γ;
}
",
            GetCA1708CSharpResultAt(2, 14, Member, GetSymbolDisplayString("C.\u03B3", "C.\u0393")));
        }

        [Fact]
        public async Task TestParametersAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace N
{
    public class C
    {
        int x;
        public delegate void Delegate(int a, int A);
        public void Method(int b, int B) {}
        public C(int d, int D)
        {
        }
        public static int operator +(C e, int E) { return 1; }
        public int X
        {
            get { return x; }
            set { x = value; }
        }
        public int this[int f, int F]
        {
            get { return x; }
            set { }
        }
        public System.Action<int, int> action = (a, A) => { };
    }
    public partial class D
    {
    }
    public partial class D
    {
        public delegate void SomeDelegate(int x, int X);
    }
}
",
            GetCA1708CSharpResultAt(7, 30, Parameter, "N.C.Delegate"),
            GetCA1708CSharpResultAt(8, 21, Parameter, "N.C.Method(int, int)"),
            GetCA1708CSharpResultAt(9, 16, Parameter, "N.C.C(int, int)"),
            GetCA1708CSharpResultAt(12, 36, Parameter, "N.C.operator +(N.C, int)"),
            GetCA1708CSharpResultAt(18, 20, Parameter, "N.C.this[int, int]"),
            GetCA1708CSharpResultAt(30, 30, Parameter, "N.D.SomeDelegate"));
        }

        #endregion

        #region Helper Methods

        private const string Namespace = IdentifiersShouldDifferByMoreThanCaseAnalyzer.Namespace;
        private const string Type = IdentifiersShouldDifferByMoreThanCaseAnalyzer.Type;
        private const string Member = IdentifiersShouldDifferByMoreThanCaseAnalyzer.Member;
        private const string Parameter = IdentifiersShouldDifferByMoreThanCaseAnalyzer.Parameter;

        private static string GetSymbolDisplayString(params string[] objectName)
        {
            return string.Join(", ", objectName.OrderByDescending(x => x, StringComparer.InvariantCulture));
        }

        private static string GetSymbolDisplayStringNoSorting(params string[] objectName)
        {
            return string.Join(", ", objectName);
        }

        private static DiagnosticResult GetGlobalCA1708CSharpResult(string typeName, string objectName)
            => VerifyCS.Diagnostic()
                .WithNoLocation()
                .WithArguments(typeName, objectName);

        private static DiagnosticResult GetCA1708CSharpResultAt(int line, int column, string typeName, string objectName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName, objectName);

        private static DiagnosticResult GetCA1708CSharpResultAt(string typeName, string objectName, params (string file, int line, int column)[] locations)
        {
            var diagnosticResult = VerifyCS.Diagnostic().WithArguments(typeName, objectName);

            foreach (var (file, line, column) in locations)
            {
#pragma warning disable RS0030 // Do not used banned APIs
                diagnosticResult = diagnosticResult.WithLocation(file, line, column);
#pragma warning restore RS0030 // Do not used banned APIs
            }

            return diagnosticResult;
        }

        #endregion
    }
}
