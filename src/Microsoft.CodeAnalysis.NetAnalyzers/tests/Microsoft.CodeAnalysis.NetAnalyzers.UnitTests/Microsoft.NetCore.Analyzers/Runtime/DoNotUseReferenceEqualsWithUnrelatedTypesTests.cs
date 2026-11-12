// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotUseReferenceEqualsWithUnrelatedTypesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotUseReferenceEqualsWithUnrelatedTypesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    [TestClass]
    public class DoNotUseReferenceEqualsWithUnrelatedTypesTests
    {
        [TestMethod]
        public async Task StringAndArray_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                class C
                {
                    bool M(string s, int[] a) => {|#0:ReferenceEquals(s, a)|};
                }
                """,
                VerifyCS.Diagnostic(DoNotUseReferenceEqualsWithUnrelatedTypesAnalyzer.Rule).WithLocation(0).WithArguments("string", "int[]"));

            await VerifyVB.VerifyAnalyzerAsync("""
                Class C
                    Function M(s As String, a As Integer()) As Boolean
                        Return {|#0:ReferenceEquals(s, a)|}
                    End Function
                End Class
                """,
                VerifyVB.Diagnostic(DoNotUseReferenceEqualsWithUnrelatedTypesAnalyzer.Rule).WithLocation(0).WithArguments("String", "Integer()"));
        }

        [TestMethod]
        public async Task UnrelatedClasses_QualifiedCall_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                class A { }
                class B { }

                class C
                {
                    bool M(A a, B b) => {|#0:object.ReferenceEquals(a, b)|};
                }
                """,
                VerifyCS.Diagnostic(DoNotUseReferenceEqualsWithUnrelatedTypesAnalyzer.Rule).WithLocation(0).WithArguments("A", "B"));
        }

        [TestMethod]
        public async Task CastToObjectDoesNotHideTheTypes_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                class C
                {
                    bool M(string s, int[] a) => {|#0:ReferenceEquals((object)s, (object)a)|};
                }
                """,
                VerifyCS.Diagnostic(DoNotUseReferenceEqualsWithUnrelatedTypesAnalyzer.Rule).WithLocation(0).WithArguments("string", "int[]"));
        }

        [TestMethod]
        public async Task SealedClassAndInterfaceItDoesNotImplement_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                interface I { }
                sealed class S { }

                class C
                {
                    bool M(S s, I i) => {|#0:ReferenceEquals(s, i)|};
                }
                """,
                VerifyCS.Diagnostic(DoNotUseReferenceEqualsWithUnrelatedTypesAnalyzer.Rule).WithLocation(0).WithArguments("S", "I"));
        }

        [TestMethod]
        public async Task UnrelatedDelegates_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    bool M(Action a, Func<int> f) => {|#0:ReferenceEquals(a, f)|};
                }
                """,
                VerifyCS.Diagnostic(DoNotUseReferenceEqualsWithUnrelatedTypesAnalyzer.Rule).WithLocation(0).WithArguments("System.Action", "System.Func<int>"));
        }

        [TestMethod]
        public async Task NullableAnnotationsGenericsAndRecords_DiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = """
                    #nullable enable
                    using System.Collections.Generic;

                    record R1;
                    record R2;

                    class C
                    {
                        bool M1(string? s, int[]? a) => {|#0:ReferenceEquals(s, a)|};
                        bool M2(List<int> x, List<string> y) => {|#1:ReferenceEquals(x, y)|};
                        bool M3(R1 x, R2 y) => {|#2:ReferenceEquals(x, y)|};
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp9,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(DoNotUseReferenceEqualsWithUnrelatedTypesAnalyzer.Rule).WithLocation(0).WithArguments("string?", "int[]?"),
                    VerifyCS.Diagnostic(DoNotUseReferenceEqualsWithUnrelatedTypesAnalyzer.Rule).WithLocation(1).WithArguments("System.Collections.Generic.List<int>", "System.Collections.Generic.List<string>"),
                    VerifyCS.Diagnostic(DoNotUseReferenceEqualsWithUnrelatedTypesAnalyzer.Rule).WithLocation(2).WithArguments("R1", "R2"),
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task VisualBasicQualifiedCallAndRelatedTypes_Async()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Imports System.Collections.Generic

                Class A
                End Class

                Class B
                End Class

                Class C
                    Function M1(x As A, y As B) As Boolean
                        Return {|#0:Object.ReferenceEquals(x, y)|}
                    End Function

                    Function M2(x As String(), y As Object()) As Boolean
                        Return ReferenceEquals(x, y)
                    End Function

                    Function M3(x As Integer(), y As IList(Of Integer)) As Boolean
                        Return ReferenceEquals(x, y)
                    End Function
                End Class
                """,
                VerifyVB.Diagnostic(DoNotUseReferenceEqualsWithUnrelatedTypesAnalyzer.Rule).WithLocation(0).WithArguments("A", "B"));
        }

        [TestMethod]
        public async Task ArraysAndTheirInterfacesAndDelegateBase_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;
                using System.Collections.Generic;

                class C
                {
                    bool M1(int[] a, IList<int> l) => ReferenceEquals(a, l);
                    bool M2(string[] a, IReadOnlyList<object> l) => ReferenceEquals(a, l);
                    bool M3(Action a, Delegate d) => ReferenceEquals(a, d);
                    bool M4(string s, IComparable c) => ReferenceEquals(s, c);
                }
                """);
        }

        [TestMethod]
        public async Task BaseAndDerived_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Security.Cryptography;

                class C
                {
                    bool M1(AsymmetricAlgorithm a, RSA r) => ReferenceEquals(a, r);
                    bool M2(AsymmetricAlgorithm a, RSA r) => ReferenceEquals(r, a);
                }
                """);

            await VerifyVB.VerifyAnalyzerAsync("""
                Imports System.Security.Cryptography

                Class C
                    Function M(a As AsymmetricAlgorithm, r As RSA) As Boolean
                        Return ReferenceEquals(a, r)
                    End Function
                End Class
                """);
        }

        [TestMethod]
        public async Task SameTypeObjectAndNull_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                class C
                {
                    bool M1(string a, string b) => ReferenceEquals(a, b);
                    bool M2(string a, object o) => ReferenceEquals(a, o);
                    bool M3(string a) => ReferenceEquals(a, null);
                    bool M4(int[] a, System.Array b) => ReferenceEquals(a, b);
                }
                """);
        }

        [TestMethod]
        public async Task InterfacesAndNonSealedClasses_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;
                using System.Collections;
                using System.Collections.Generic;

                class Open { }

                class C
                {
                    bool M1(Open o, IDisposable d) => ReferenceEquals(o, d);
                    bool M2(IDisposable d, IEnumerable e) => ReferenceEquals(d, e);
                    bool M3(IEnumerable<string> s, IEnumerable<object> o) => ReferenceEquals(s, o);
                    bool M4(string s, IEnumerable<char> e) => ReferenceEquals(s, e);
                }
                """);
        }

        [TestMethod]
        public async Task CovariantArrays_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                class C
                {
                    bool M(string[] s, object[] o) => ReferenceEquals(s, o);
                }
                """);
        }

        [TestMethod]
        public async Task TypeParametersAndDynamic_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                class C
                {
                    bool M1<T>(T t, string s) where T : class => ReferenceEquals(t, s);
                    bool M2(dynamic d, string s) => ReferenceEquals(d, s);
                }
                """);
        }

        [TestMethod]
        public async Task GenericTypesBuiltFromTypeParameters_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;

                class C<TElement>
                {
                    static readonly Action<object> s_callback = _ => { };
                    static readonly Func<TElement, TElement> s_identity = x => x;

                    bool M1<TState>(Action<TState> callback) => ReferenceEquals(callback, s_callback);
                    bool M2<TKey>(Func<TElement, TKey> keySelector) => ReferenceEquals(keySelector, s_identity);
                    bool M3<TBase, TDerived>(TBase[] b, TDerived[] d) where TDerived : class, TBase => ReferenceEquals(b, d);
                }
                """);
        }

        [TestMethod]
        public async Task NestedTypeOfGenericBuiltFromTypeParameter_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                class Outer<T>
                {
                    public class Inner { }
                }

                class C
                {
                    bool M<T>(Outer<T>.Inner a, Outer<int>.Inner b) => ReferenceEquals(a, b);
                }
                """);
        }

        [TestMethod]
        public async Task PointerArrayBuiltFromTypeParameter_NoDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = """
                    unsafe class C
                    {
                        bool M<T>(T*[] a, int*[] b) where T : unmanaged => ReferenceEquals(a, b);
                    }
                    """,
                SolutionTransforms =
                {
                    (solution, projectId) => solution.WithProjectCompilationOptions(projectId,
                        ((CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!).WithAllowUnsafe(true))
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task UnknownTypes_NoDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = """
                    using System.Collections.Generic;

                    class Derived : MissingBase { }

                    class C
                    {
                        bool M1(Derived d, System.Type t) => ReferenceEquals(d, t);
                        bool M2(List<Missing> m, List<int> l) => ReferenceEquals(m, l);
                        bool M3(Missing[] m, string s) => ReferenceEquals(m, s);
                    }
                    """,
                CompilerDiagnostics = CompilerDiagnostics.None,
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task ValueTypesAreLeftToCA2013_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                class C
                {
                    bool M(int i, string s) => ReferenceEquals(i, s);
                }
                """);
        }

        [TestMethod]
        public async Task UserDefinedConversionIsNotLookedThrough_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                class A
                {
                    public static explicit operator string(A a) => "a";
                }

                class C
                {
                    bool M(A a, string s) => ReferenceEquals((string)a, s);
                }
                """);
        }

        [TestMethod]
        public async Task OtherReferenceEqualsMethods_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                class C
                {
                    static bool ReferenceEquals(string s, int[] a) => false;

                    bool M(string s, int[] a) => ReferenceEquals(s, a);
                }
                """);
        }
    }
}
