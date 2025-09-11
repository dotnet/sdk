// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Usage.CSharpPreferGenericOverloadsAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Usage.CSharpPreferGenericOverloadsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Usage.BasicPreferGenericOverloadsAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Usage.BasicPreferGenericOverloadsFixer>;

namespace Microsoft.NetCore.Analyzers.Usage.UnitTests
{
    public class PreferGenericOverloadsTests
    {
        [Fact]
        public async Task NoTypeArgument_NoDiagnostic_CS()
        {
            string source = """
                class C
                {
                    void M(int x) {}
                    void M<T>() {}

                    void Test()
                    {
                        M(0);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RuntimeTypeArgument_NoDiagnostic_CS()
        {
            string source = """
                class C
                {
                    void M(System.Type type) {}
                    void M<T>() {}

                    void Test()
                    {
                        M(GetType());
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task StaticClassAsTypeArgument_NoDiagnostic_CS()
        {
            string source = """
                static class C
                {
                    static void M(System.Type type) {}
                    static void M<T>() {}

                    static void Test()
                    {
                        M(typeof(C));
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task UnboundGenericTypeArgument_NoDiagnostic_CS()
        {
            string source = """
                class ViolatingType<T> {}

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() {}

                    void Test()
                    {
                        M(typeof(ViolatingType<>));
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact, WorkItem(7246, "https://github.com/dotnet/roslyn-analyzers/issues/7246")]
        public async Task UnboundGenericTypeArgumentWithMatchingOtherArguments_NoDiagnostic_CS()
        {
            string source = """
                class ViolatingType<T> {}

                class C
                {
                    void M(System.Type type, object other) {}
                    void M<T>() {}
                    void M(object other) {}

                    void Test()
                    {
                        M(typeof(ViolatingType<>), null);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task WrongArity_NoDiagnostic_CS()
        {
            string source = """
                class C
                {
                    void M(System.Type type1, System.Type type2) {}
                    void M<T1>() {}
                    void M<T1, T2, T3>() {}

                    void Test()
                    {
                        M(typeof(C), typeof(C));
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task WrongParameterCount_NoDiagnostic_CS()
        {
            string source = """
                class C
                {
                    void M(System.Type type, int x) {}
                    void M<T>() {}
                    void M<T>(int x, int y) {}

                    void Test()
                    {
                        M(typeof(C), 0);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task WrongParameterType_NoDiagnostic_CS()
        {
            string source = """
                class C
                {
                    void M(System.Type type, int x) {}
                    void M<T>(string x) {}

                    void Test()
                    {
                        M(typeof(C), 0);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task WrongParameterTypeWithOneMatching_NoDiagnostic_CS()
        {
            string source = """
                class C
                {
                    void M(System.Type type, int x, string y) {}
                    void M<T>(string x, string y) {}

                    void Test()
                    {
                        M(typeof(C), 0, string.Empty);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SameAsContainingSymbol_NoDiagnostic_CS()
        {
            string source = """
                class C
                {
                    object M(System.Type type, object x) { return x; }
                    T M<T>(T x) { return (T)M(typeof(T), x); }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task ViolatesValueTypeConstraint_NoDiagnostic_CS()
        {
            string source = """
                class ViolatingType {}

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() where T : struct {}

                    void Test()
                    {
                        M(typeof(ViolatingType));
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SatisfiesValueTypeConstraint_OffersFixer_CS()
        {
            string source = """
                struct ValidType {}

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() where T : struct {}

                    void Test()
                    {
                        [|M(typeof(ValidType))|];
                    }
                }
                """;

            string fixedSource = """
                struct ValidType {}

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() where T : struct {}

                    void Test()
                    {
                        M<ValidType>();
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ViolatesReferenceTypeConstraint_NoDiagnostic_CS()
        {
            string source = """
                struct ViolatingType {}

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() where T : class {}

                    void Test()
                    {
                        M(typeof(ViolatingType));
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SatisfiesReferenceTypeConstraint_OffersFixer_CS()
        {
            string source = """
                class ValidType {}

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() where T : class {}

                    void Test()
                    {
                        [|M(typeof(ValidType))|];
                    }
                }
                """;

            string fixedSource = """
                class ValidType {}

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() where T : class {}

                    void Test()
                    {
                        M<ValidType>();
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ViolatesUnmanagedTypeConstraint_NoDiagnostic_CS()
        {
            string source = """
                class ViolatingType {}

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() where T : unmanaged {}

                    void Test()
                    {
                        M(typeof(ViolatingType));
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SatisfiesUnmanagedTypeConstraint_OffersFixer_CS()
        {
            string source = """
                struct ValidType {}

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() where T : unmanaged {}

                    void Test()
                    {
                        [|M(typeof(ValidType))|];
                    }
                }
                """;

            string fixedSource = """
                struct ValidType {}

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() where T : unmanaged {}

                    void Test()
                    {
                        M<ValidType>();
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ViolatesConstructorConstraint_NoDiagnostic_CS()
        {
            string source = """
                class ViolatingType
                {
                    private ViolatingType() {}
                }

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() where T : new() {}

                    void Test()
                    {
                        M(typeof(ViolatingType));
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SatisfiesConstructorConstraint_OffersFixer_CS()
        {
            string source = """
                class ValidType
                {
                    public ValidType() {}
                }

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() where T : new() {}

                    void Test()
                    {
                        [|M(typeof(ValidType))|];
                    }
                }
                """;

            string fixedSource = """
                class ValidType
                {
                    public ValidType() {}
                }

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() where T : new() {}

                    void Test()
                    {
                        M<ValidType>();
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ViolatesTypeConstraint_NoDiagnostic_CS()
        {
            string source = """
                class ViolatingType {}

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() where T : C {}

                    void Test()
                    {
                        M(typeof(ViolatingType));
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SatisfiesTypeConstraint_OffersFixer_CS()
        {
            string source = """
                class ValidType : C {}

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() where T : C {}

                    void Test()
                    {
                        [|M(typeof(ValidType))|];
                    }
                }
                """;

            string fixedSource = """
                class ValidType : C {}

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() where T : C {}

                    void Test()
                    {
                        M<ValidType>();
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact, WorkItem(7245, "https://github.com/dotnet/roslyn-analyzers/issues/7245")]
        public async Task ViolatesNullabilityConstraint_NoDiagnostic_CS()
        {
            string source = """
                #nullable enable

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() where T : notnull {}

                    void Test<T>()
                    {
                        M(typeof(T));
                    }
                }
                """;

            var test = new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = source,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9
            };

            await test.RunAsync();
        }

        [Fact, WorkItem(7245, "https://github.com/dotnet/roslyn-analyzers/issues/7245")]
        public async Task ViolatesNullabilityConstraintNullableDisabled_NoDiagnostic_CS()
        {
            string source = """
                #nullable disable

                class C
                {
                    void M(System.Type type) {}
                    void M<T>() where T : notnull {}

                    void Test<T>()
                    {
                        M(typeof(T));
                    }
                }
                """;

            var test = new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = source,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task SingleTypeArgument_OffersFixer_CS()
        {
            string source = """
                class C
                {
                    void M(System.Type type) {}
                    void M<T>() {}

                    void Test()
                    {
                        [|M(typeof(C))|];
                    }
                }
                """;

            string fixedSource = """
                class C
                {
                    void M(System.Type type) {}
                    void M<T>() {}

                    void Test()
                    {
                        M<C>();
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task MultipleTypeArguments_OffersFixer_CS()
        {
            string source = """
                class A {}
                class B {}

                class C
                {
                    void M(System.Type type1, System.Type type2, System.Type type3) {}
                    void M<T1, T2, T3>() {}

                    void Test()
                    {
                        [|M(typeof(A), typeof(B), typeof(C))|];
                    }
                }
                """;

            string fixedSource = """
                class A {}
                class B {}

                class C
                {
                    void M(System.Type type1, System.Type type2, System.Type type3) {}
                    void M<T1, T2, T3>() {}

                    void Test()
                    {
                        M<A, B, C>();
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task SingleTypeArgumentWithOtherArgument_OffersFixer_CS()
        {
            string source = """
                class C
                {
                    void M(System.Type type, int x) {}
                    void M<T>(int x) {}

                    void Test()
                    {
                        [|M(typeof(C), 0)|];
                    }
                }
                """;

            string fixedSource = """
                class C
                {
                    void M(System.Type type, int x) {}
                    void M<T>(int x) {}

                    void Test()
                    {
                        M<C>(0);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task MultipleTypeArgumentsWithOtherArgument_OffersFixer_CS()
        {
            string source = """
                class A {}
                class B {}

                class C
                {
                    void M(System.Type type1, System.Type type2, System.Type type3, int x) {}
                    void M<T1, T2, T3>(int x) {}

                    void Test()
                    {
                        [|M(typeof(A), typeof(B), typeof(C), 0)|];
                    }
                }
                """;

            string fixedSource = """
                class A {}
                class B {}

                class C
                {
                    void M(System.Type type1, System.Type type2, System.Type type3, int x) {}
                    void M<T1, T2, T3>(int x) {}

                    void Test()
                    {
                        M<A, B, C>(0);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task SingleTypeArgumentWithOtherArguments_OffersFixer_CS()
        {
            string source = """
                class C
                {
                    void M(System.Type type, int x, string y, object z) {}
                    void M<T>(int x, string y, object z) {}

                    void Test()
                    {
                        [|M(typeof(C), 0, "Test", this)|];
                    }
                }
                """;

            string fixedSource = """
                class C
                {
                    void M(System.Type type, int x, string y, object z) {}
                    void M<T>(int x, string y, object z) {}

                    void Test()
                    {
                        M<C>(0, "Test", this);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task MultipleTypeArgumentsWithOtherArguments_OffersFixer_CS()
        {
            string source = """
                class A {}
                class B {}

                class C
                {
                    void M(System.Type type1, System.Type type2, System.Type type3, int x, string y, object z) {}
                    void M<T1, T2, T3>(int x, string y, object z) {}

                    void Test()
                    {
                        [|M(typeof(A), typeof(B), typeof(C), 0, "Test", this)|];
                    }
                }
                """;

            string fixedSource = """
                class A {}
                class B {}

                class C
                {
                    void M(System.Type type1, System.Type type2, System.Type type3, int x, string y, object z) {}
                    void M<T1, T2, T3>(int x, string y, object z) {}

                    void Test()
                    {
                        M<A, B, C>(0, "Test", this);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task SingleTypeArgumentWithOtherGenericArgument_OffersFixer_CS()
        {
            string source = """
                class C
                {
                    void M(System.Type type, object x) {}
                    void M<T>(T x) {}

                    void Test()
                    {
                        [|M(typeof(C), new C())|];
                    }
                }
                """;

            string fixedSource = """
                class C
                {
                    void M(System.Type type, object x) {}
                    void M<T>(T x) {}

                    void Test()
                    {
                        M(new C());
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task MultipleTypeArgumentsWithOtherGenericArgument_OffersFixer_CS()
        {
            string source = """
                class A {}
                class B {}

                class C
                {
                    void M(System.Type type1, System.Type type2, System.Type type3, object x) {}
                    void M<T1, T2, T3>(T1 x) {}

                    void Test()
                    {
                        [|M(typeof(A), typeof(B), typeof(C), new A())|];
                    }
                }
                """;

            string fixedSource = """
                class A {}
                class B {}

                class C
                {
                    void M(System.Type type1, System.Type type2, System.Type type3, object x) {}
                    void M<T1, T2, T3>(T1 x) {}

                    void Test()
                    {
                        M<A, B, C>(new A());
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task TypeParameterNotFirst_OffersFixer_CS()
        {
            string source = """
                class C
                {
                    void M(int x, System.Type type) {}
                    void M<T>(int x) {}

                    void Test()
                    {
                        [|M(0, typeof(C))|];
                    }
                }
                """;

            string fixedSource = """
                class C
                {
                    void M(int x, System.Type type) {}
                    void M<T>(int x) {}

                    void Test()
                    {
                        M<C>(0);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ExtensionMethod_OffersFixer_CS()
        {
            string source = """
                public static class CExtensions
                {
                    public static void M(this C c, System.Type type) {}
                    public static void M<T>(this C c) {}
                }

                public class C
                {
                    void Test()
                    {
                        [|new C().M(typeof(C))|];
                    }
                }
                """;

            string fixedSource = """
                public static class CExtensions
                {
                    public static void M(this C c, System.Type type) {}
                    public static void M<T>(this C c) {}
                }

                public class C
                {
                    void Test()
                    {
                        new C().M<C>();
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ExtensionMethodCalledDirectly_OffersFixer_CS()
        {
            string source = """
                public static class CExtensions
                {
                    public static void M(this C c, System.Type type) {}
                    public static void M<T>(this C c) {}
                }

                public class C
                {
                    void Test()
                    {
                        [|CExtensions.M(this, typeof(C))|];
                    }
                }
                """;

            string fixedSource = """
                public static class CExtensions
                {
                    public static void M(this C c, System.Type type) {}
                    public static void M<T>(this C c) {}
                }

                public class C
                {
                    void Test()
                    {
                        CExtensions.M<C>(this);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task TypeAlias_OffersFixer_CS()
        {
            string source = """
                using A = C;

                class C
                {
                    void M(System.Type type, object x) {}
                    void M<T>(T x) {}

                    void Test()
                    {
                        [|M(typeof(A), new A())|];
                    }
                }
                """;

            string fixedSource = """
                using A = C;

                class C
                {
                    void M(System.Type type, object x) {}
                    void M<T>(T x) {}

                    void Test()
                    {
                        M(new A());
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task OptionalParameters_OffersFixer_CS()
        {
            string source = """
                class C
                {
                    void M(System.Type type, int x, int y = 1, int z = 2) {}
                    void M<T>(int x, int y = 1, int z = 2) {}

                    void Test()
                    {
                        [|M(typeof(C), 0, 5)|];
                    }
                }
                """;

            string fixedSource = """
                class C
                {
                    void M(System.Type type, int x, int y = 1, int z = 2) {}
                    void M<T>(int x, int y = 1, int z = 2) {}

                    void Test()
                    {
                        M<C>(0, 5);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task StaticMethods_OffersFixer_CS()
        {
            string source = """
                class C
                {
                    static void M(System.Type type, object x) {}
                    static void M<T>(T x) {}

                    void Test()
                    {
                        [|M(typeof(C), this)|];
                    }
                }
                """;

            string fixedSource = """
                class C
                {
                    static void M(System.Type type, object x) {}
                    static void M<T>(T x) {}

                    void Test()
                    {
                        M(this);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task StaticMethodsInStaticClass_OffersFixer_CS()
        {
            string source = """
                public static class StaticClass
                {
                    public static void M(System.Type type, object x) {}
                    public static void M<T>(T x) {}
                }

                class C
                {
                    void Test()
                    {
                        [|StaticClass.M(typeof(C), this)|];
                    }
                }
                """;

            string fixedSource = """
                public static class StaticClass
                {
                    public static void M(System.Type type, object x) {}
                    public static void M<T>(T x) {}
                }

                class C
                {
                    void Test()
                    {
                        StaticClass.M(this);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task StaticMethodsInStaticClassWithNamespace_OffersFixer_CS()
        {
            string source = """
                namespace TestNamespace
                {
                    public static class StaticClass
                    {
                        public static void M(System.Type type, object x) {}
                        public static void M<T>(T x) {}
                    }
                }

                class C
                {
                    void Test()
                    {
                        [|TestNamespace.StaticClass.M(typeof(C), this)|];
                    }
                }
                """;

            string fixedSource = """
                namespace TestNamespace
                {
                    public static class StaticClass
                    {
                        public static void M(System.Type type, object x) {}
                        public static void M<T>(T x) {}
                    }
                }

                class C
                {
                    void Test()
                    {
                        TestNamespace.StaticClass.M(this);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ReturnTypeIsNotCompatible_NoDiagnostic_CS()
        {
            string source = """
                class C
                {
                    void Test()
                    {
                        System.Collections.Immutable.ImmutableHashSet<System.Type> x = System.Collections.Immutable.ImmutableHashSet.Create(typeof(C));
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task ReturnTypeIsIgnoredForExpressionStatement_OffersFixer_CS()
        {
            string source = """
                class C
                {
                    void Test()
                    {
                        [|System.Collections.Immutable.ImmutableHashSet.Create(typeof(C))|];
                    }
                }
                """;

            string fixedSource = """
                class C
                {
                    void Test()
                    {
                        System.Collections.Immutable.ImmutableHashSet.Create<C>();
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task UnnecessaryCastIsRemoved_OffersFixer_CS()
        {
            string source = """
                class C
                {
                    object M(System.Type type, object x) { return x; }
                    T M<T>(T x) { return x; }

                    C Test()
                    {
                        return (C)[|M(typeof(C), this)|];
                    }
                }
                """;

            string fixedSource = """
                class C
                {
                    object M(System.Type type, object x) { return x; }
                    T M<T>(T x) { return x; }

                    C Test()
                    {
                        return M(this);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task NeededCastIsPreserved_OffersFixer_CS()
        {
            string source = """
                class C
                {
                    object M(System.Type type, object x) { return x; }
                    object M<T>(T x) { return x; }

                    C Test()
                    {
                        return (C)[|M(typeof(C), this)|];
                    }
                }
                """;

            string fixedSource = """
                class C
                {
                    object M(System.Type type, object x) { return x; }
                    object M<T>(T x) { return x; }

                    C Test()
                    {
                        return (C)[|M(this)|];
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task UnaryPostfixOperatorIsPreserved_OffersFixer_CS()
        {
            string source = """
                #nullable enable

                using System;
                using System.Reflection;

                class C
                {
                    object? M(System.Type type, object x) { return x; }
                    T? M<T>(T x) { return x; }
                
                    C Test()
                    {
                        var a = (Func<C>)[|typeof(C).GetMethod("M", BindingFlags.Instance | BindingFlags.NonPublic)!.CreateDelegate(typeof(Func<C>))|];
                        return (C)[|M(typeof(C), this)|]!;
                    }
                }
                """;

            string fixedSource = """
                #nullable enable

                using System;
                using System.Reflection;

                class C
                {
                    object? M(System.Type type, object x) { return x; }
                    T? M<T>(T x) { return x; }
                
                    C Test()
                    {
                        var a = typeof(C).GetMethod("M", BindingFlags.Instance | BindingFlags.NonPublic)!.CreateDelegate<Func<C>>();
                        return M(this)!;
                    }
                }
                """;

            var test = new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = fixedSource,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task NullConditionalMemberAccessOperatorIsPreserved_OffersFixer_CS()
        {
            string source = """
                #nullable enable

                class C
                {
                    object? M(System.Type type, object x) { return x; }
                    T? M<T>(T x) { return x; }
                
                    C? Test()
                    {
                        return ((C?)[|M(typeof(C), this)|])?.Other();
                    }

                    C Other() { return new C(); }
                }
                """;

            string fixedSource = """
                #nullable enable

                class C
                {
                    object? M(System.Type type, object x) { return x; }
                    T? M<T>(T x) { return x; }
                
                    C? Test()
                    {
                        return M(this)?.Other();
                    }
                
                    C Other() { return new C(); }
                }
                """;

            var test = new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = fixedSource,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task NamedParametersArePreserved_OffersFixer_CS()
        {
            string source = """
                class C
                {
                    void M(System.Type type, int x) {}
                    void M<T>(int x) {}

                    void Test()
                    {
                        [|M(x: 0, type: typeof(C))|];
                    }
                }
                """;

            string fixedSource = """
                class C
                {
                    void M(System.Type type, int x) {}
                    void M<T>(int x) {}

                    void Test()
                    {
                        M<C>(x: 0);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task TriviaIsPreserved_OffersFixer_CS()
        {
            string source = """
                class C
                {
                    void M(System.Type type) {}
                    void M<T>() {}

                    void Test()
                    {
                        // reticulates the splines
                        [|M(typeof(C))|];
                    }
                }
                """;

            string fixedSource = """
                class C
                {
                    void M(System.Type type) {}
                    void M<T>() {}

                    void Test()
                    {
                        // reticulates the splines
                        M<C>();
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task TriviaIsPreservedWhenCastIsRemoved_OffersFixer_CS()
        {
            string source = """
                class C
                {
                    object M(System.Type type, object x) { return x; }
                    T M<T>(T x) { return x; }

                    C M()
                    {
                        // reticulates the splines
                        return (C)[|M(typeof(C), this)|];
                    }
                }
                """;

            string fixedSource = """
                class C
                {
                    object M(System.Type type, object x) { return x; }
                    T M<T>(T x) { return x; }

                    C M()
                    {
                        // reticulates the splines
                        return M(this);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task NoTypeArgument_NoDiagnostic_VB()
        {
            string source = """
                Class C
                    Sub M(x as Integer) : End Sub
                    Sub M(Of T)() : End Sub

                    Sub Test()
                        M(0)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RuntimeTypeArgument_NoDiagnostic_VB()
        {
            string source = """
                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T)() : End Sub

                    Sub Test()
                        M(Me.GetType())
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task UnboundGenericTypeArgument_NoDiagnostic_VB()
        {
            string source = """
                Class ViolatingType(Of T) : End Class

                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T)() : End Sub

                    Sub Test()
                        M(GetType(ViolatingType(Of )))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact, WorkItem(7246, "https://github.com/dotnet/roslyn-analyzers/issues/7246")]
        public async Task UnboundGenericTypeArgumentWithMatchingOtherArguments_NoDiagnostic_VB()
        {
            string source = """
                Class ViolatingType(Of T) : End Class
                
                Class C
                    Sub M(type as System.Type, other as Object) : End Sub
                    Sub M(Of T)() : End Sub
                    Sub M(other as Object) : End Sub

                    Sub Test()
                        M(GetType(ViolatingType(Of )))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task WrongArity_NoDiagnostic_VB()
        {
            string source = """
                Class C
                    Sub M(type1 as System.Type, type2 as System.Type) : End Sub
                    Sub M(Of T1)() : End Sub
                    Sub M(Of T1, T2, T3)() : End Sub

                    Sub Test()
                        M(GetType(C), GetType(C))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task WrongParameterCount_NoDiagnostic_VB()
        {
            string source = """
                Class C
                    Sub M(type as System.Type, x as Integer) : End Sub
                    Sub M(Of T)() : End Sub
                    Sub M(Of T)(x as Integer, y as Integer) : End Sub

                    Sub Test()
                        M(GetType(C), 0)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task WrongParameterType_NoDiagnostic_VB()
        {
            string source = """
                Class C
                    Sub M(type as System.Type, x as Integer) : End Sub
                    Sub M(Of T)(x as String) : End Sub

                    Sub Test()
                        M(GetType(C), 0)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task WrongParameterTypeWithOneMatching_NoDiagnostic_VB()
        {
            string source = """
                Class C
                    Sub M(type as System.Type, x as Integer, y as String) : End Sub
                    Sub M(Of T)(x as String, y as String) : End Sub

                    Sub Test()
                        M(GetType(C), 0, String.Empty)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SameAsContainingSymbol_NoDiagnostic_VB()
        {
            string source = """
                Class C
                    Function M(type as System.Type, x as Object) as Object
                        Return x
                    End Function

                    Function M(Of T)(x as Object) as T
                        Return CType(M(GetType(T), x), T)
                    End Function
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task ViolatesValueTypeConstraint_NoDiagnostic_VB()
        {
            string source = """
                Class ViolatingType : End Class

                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T as Structure)() : End Sub

                    Sub Test()
                        M(GetType(ViolatingType))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SatisfiesValueTypeConstraint_OffersFixer_VB()
        {
            string source = """
                Structure ValidType : End Structure

                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T as Structure)() : End Sub

                    Sub Test()
                        [|M(GetType(ValidType))|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Structure ValidType : End Structure

                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T as Structure)() : End Sub

                    Sub Test()
                        M(Of ValidType)()
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ViolatesReferenceTypeConstraint_NoDiagnostic_VB()
        {
            string source = """
                Structure ViolatingType : End Structure

                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T as Class)() : End Sub

                    Sub Test()
                        M(GetType(ViolatingType))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SatisfiesReferenceTypeConstraint_OffersFixer_VB()
        {
            string source = """
                Class ValidType : End Class

                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T as Class)() : End Sub

                    Sub Test()
                        [|M(GetType(ValidType))|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class ValidType : End Class

                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T as Class)() : End Sub

                    Sub Test()
                        M(Of ValidType)()
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ViolatesConstructorConstraint_NoDiagnostic_VB()
        {
            string source = """
                Class ViolatingType
                    Private Sub New() : End Sub
                End Class

                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T as New)() : End Sub

                    Sub Test()
                        M(GetType(ViolatingType))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SatisfiesConstructorConstraint_OffersFixer_VB()
        {
            string source = """
                Class ValidType
                    Public Sub New() : End Sub
                End Class

                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T as New)() : End Sub

                    Sub Test()
                        [|M(GetType(ValidType))|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class ValidType
                    Public Sub New() : End Sub
                End Class

                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T as New)() : End Sub

                    Sub Test()
                        M(Of ValidType)()
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ViolatesTypeConstraint_NoDiagnostic_VB()
        {
            string source = """
                Class ViolatingType : End Class

                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T as C)() : End Sub

                    Sub Test()
                        M(GetType(ViolatingType))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SatisfiesTypeConstraint_OffersFixer_VB()
        {
            string source = """
                Class ValidType
                    Inherits C
                End Class

                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T as C)() : End Sub

                    Sub Test()
                        [|M(GetType(ValidType))|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class ValidType
                    Inherits C
                End Class

                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T as C)() : End Sub

                    Sub Test()
                        M(Of ValidType)()
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task SingleTypeArgument_OffersFixer_VB()
        {
            string source = """
                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T)() : End Sub

                    Sub Test()
                        [|M(GetType(C))|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T)() : End Sub

                    Sub Test()
                        M(Of C)()
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task MultipleTypeArguments_OffersFixer_VB()
        {
            string source = """
                Class A : End Class
                Class B : End Class

                Class C
                    Sub M(type1 as System.Type, type2 as System.Type, type3 as System.Type) : End Sub
                    Sub M(Of T1, T2, T3)() : End Sub

                    Sub Test()
                        [|M(GetType(A), GetType(B), GetType(C))|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class A : End Class
                Class B : End Class

                Class C
                    Sub M(type1 as System.Type, type2 as System.Type, type3 as System.Type) : End Sub
                    Sub M(Of T1, T2, T3)() : End Sub

                    Sub Test()
                        M(Of A, B, C)()
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task SingleTypeArgumentWithOtherArgument_OffersFixer_VB()
        {
            string source = """
                Class C
                    Sub M(type as System.Type, x as Integer) : End Sub
                    Sub M(Of T)(x as Integer) : End Sub

                    Sub Test()
                        [|M(GetType(C), 0)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class C
                    Sub M(type as System.Type, x as Integer) : End Sub
                    Sub M(Of T)(x as Integer) : End Sub

                    Sub Test()
                        M(Of C)(0)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task MultipleTypeArgumentsWithOtherArgument_OffersFixer_VB()
        {
            string source = """
                Class A : End Class
                Class B : End Class

                Class C
                    Sub M(type1 as System.Type, type2 as System.Type, type3 as System.Type, x as Integer) : End Sub
                    Sub M(Of T1, T2, T3)(x as Integer) : End Sub

                    Sub Test()
                        [|M(GetType(A), GetType(B), GetType(C), 0)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class A : End Class
                Class B : End Class

                Class C
                    Sub M(type1 as System.Type, type2 as System.Type, type3 as System.Type, x as Integer) : End Sub
                    Sub M(Of T1, T2, T3)(x as Integer) : End Sub

                    Sub Test()
                        M(Of A, B, C)(0)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task SingleTypeArgumentWithOtherArguments_OffersFixer_VB()
        {
            string source = """
                Class C
                    Sub M(type as System.Type, x as Integer, y as String, z as Object) : End Sub
                    Sub M(Of T)(x as Integer, y as String, z as Object) : End Sub

                    Sub Test()
                        [|M(GetType(C), 0, "Test", Me)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class C
                    Sub M(type as System.Type, x as Integer, y as String, z as Object) : End Sub
                    Sub M(Of T)(x as Integer, y as String, z as Object) : End Sub

                    Sub Test()
                        M(Of C)(0, "Test", Me)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task MultipleTypeArgumentsWithOtherArguments_OffersFixer_VB()
        {
            string source = """
                Class A : End Class
                Class B : End Class

                Class C
                    Sub M(type1 as System.Type, type2 as System.Type, type3 as System.Type, x as Integer, y as String, z as Object) : End Sub
                    Sub M(Of T1, T2, T3)(x as Integer, y as String, z as Object) : End Sub

                    Sub Test()
                        [|M(GetType(A), GetType(B), GetType(C), 0, "Test", Me)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class A : End Class
                Class B : End Class

                Class C
                    Sub M(type1 as System.Type, type2 as System.Type, type3 as System.Type, x as Integer, y as String, z as Object) : End Sub
                    Sub M(Of T1, T2, T3)(x as Integer, y as String, z as Object) : End Sub

                    Sub Test()
                        M(Of A, B, C)(0, "Test", Me)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task SingleTypeArgumentWithOtherGenericArgument_OffersFixer_VB()
        {
            string source = """
                Class C
                    Sub M(type as System.Type, x as Object) : End Sub
                    Sub M(Of T)(x as T) : End Sub

                    Sub Test()
                        [|M(GetType(C), new C())|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class C
                    Sub M(type as System.Type, x as Object) : End Sub
                    Sub M(Of T)(x as T) : End Sub
                
                    Sub Test()
                        M(new C())
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task MultipleTypeArgumentsWithOtherGenericArgument_OffersFixer_VB()
        {
            string source = """
                Class A : End Class
                Class B : End Class

                Class C
                    Sub M(type1 as System.Type, type2 as System.Type, type3 as System.Type, x as Object) : End Sub
                    Sub M(Of T1, T2, T3)(x as T1) : End Sub

                    Sub Test()
                        [|M(GetType(A), GetType(B), GetType(C), new A())|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class A : End Class
                Class B : End Class

                Class C
                    Sub M(type1 as System.Type, type2 as System.Type, type3 as System.Type, x as Object) : End Sub
                    Sub M(Of T1, T2, T3)(x as T1) : End Sub

                    Sub Test()
                        M(Of A, B, C)(new A())
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task TypeParameterNotFirst_OffersFixer_VB()
        {
            string source = """
                Class C
                    Sub M(x as Integer, type as System.Type) : End Sub
                    Sub M(Of T)(x as Integer) : End Sub

                    Sub Test()
                        [|M(0, GetType(C))|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class C
                    Sub M(x as Integer, type as System.Type) : End Sub
                    Sub M(Of T)(x as Integer) : End Sub

                    Sub Test()
                        M(Of C)(0)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ExtensionMethod_OffersFixer_VB()
        {
            string source = """
                Module CExtensions
                    <System.Runtime.CompilerServices.Extension()>
                    Public Sub M(c as C, type as System.Type) : End Sub
                    
                    <System.Runtime.CompilerServices.Extension()>
                    Public Sub M(Of T)(C as C) : End Sub
                End Module

                Class C
                    Sub Test()
                        Dim c as C = new C()
                        [|c.M(GetType(C))|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Module CExtensions
                    <System.Runtime.CompilerServices.Extension()>
                    Public Sub M(c as C, type as System.Type) : End Sub
                    
                    <System.Runtime.CompilerServices.Extension()>
                    Public Sub M(Of T)(C as C) : End Sub
                End Module
                
                Class C
                    Sub Test()
                        Dim c as C = new C()
                        c.M(Of C)()
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ExtensionMethodCalledDirectly_OffersFixer_VB()
        {
            string source = """
                Module CExtensions
                    <System.Runtime.CompilerServices.Extension()>
                    Public Sub M(c as C, type as System.Type) : End Sub
                    
                    <System.Runtime.CompilerServices.Extension()>
                    Public Sub M(Of T)(C as C) : End Sub
                End Module

                Class C
                    Sub Test()
                        Dim c as C = new C()
                        [|CExtensions.M(c, GetType(C))|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Module CExtensions
                    <System.Runtime.CompilerServices.Extension()>
                    Public Sub M(c as C, type as System.Type) : End Sub
                    
                    <System.Runtime.CompilerServices.Extension()>
                    Public Sub M(Of T)(C as C) : End Sub
                End Module
                
                Class C
                    Sub Test()
                        Dim c as C = new C()
                        CExtensions.M(Of C)(c)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task TypeAlias_OffersFixer_VB()
        {
            string source = """
                Imports A = C

                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T)() : End Sub

                    Sub Test()
                        [|M(GetType(A))|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports A = C

                Class C
                    Sub M(type as System.Type) : End Sub
                    Sub M(Of T)() : End Sub

                    Sub Test()
                        M(Of A)()
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task OptionalParameters_OffersFixer_VB()
        {
            string source = """
                Class C
                    Sub M(type as System.Type, x as Integer, Optional y as Integer = 1, Optional z as Integer = 2) : End Sub
                    Sub M(Of T)(x as Integer, Optional y as Integer = 1, Optional z as Integer = 2) : End Sub

                    Sub Test()
                        [|M(GetType(C), 0, 5)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class C
                    Sub M(type as System.Type, x as Integer, Optional y as Integer = 1, Optional z as Integer = 2) : End Sub
                    Sub M(Of T)(x as Integer, Optional y as Integer = 1, Optional z as Integer = 2) : End Sub

                    Sub Test()
                        M(Of C)(0, 5)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task StaticMethods_OffersFixer_VB()
        {
            string source = """
                Class C
                    Shared Sub M(type as System.Type) : End Sub
                    Shared Sub M(Of T)() : End Sub

                    Sub Test()
                        [|M(GetType(C))|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class C
                    Shared Sub M(type as System.Type) : End Sub
                    Shared Sub M(Of T)() : End Sub

                    Sub Test()
                        M(Of C)()
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task StaticMethodsWithNamespace_OffersFixer_VB()
        {
            string source = """
                Namespace TestNamespace
                    Module TestModule
                        Sub M(type as System.Type) : End Sub
                        Sub M(Of T)() : End Sub
                    End Module
                End Namespace

                Class C
                    Sub Test()
                        [|TestNamespace.M(GetType(C))|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Namespace TestNamespace
                    Module TestModule
                        Sub M(type as System.Type) : End Sub
                        Sub M(Of T)() : End Sub
                    End Module
                End Namespace

                Class C
                    Sub Test()
                        TestNamespace.M(Of C)()
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ReturnTypeIsNotCompatible_NoDiagnostic_VB()
        {
            string source = """
                Class C
                    Sub Test()
                        Dim x as System.Collections.Immutable.ImmutableHashSet(Of System.Type) = System.Collections.Immutable.ImmutableHashSet.Create(GetType(C))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task ReturnTypeIsIgnoredForExpressionStatement_OffersFixer_VB()
        {
            string source = """
                Class C
                    Sub Test()
                        [|System.Collections.Immutable.ImmutableHashSet.Create(GetType(C))|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class C
                    Sub Test()
                        System.Collections.Immutable.ImmutableHashSet.Create(Of C)()
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task UnnecessaryCastIsRemoved_OffersFixer_VB()
        {
            string source = """
                Class C
                    Function M(type as System.Type, x as Object) as Object
                        Return x
                    End Function

                    Function M(Of T)(x as T) as T
                        Return x
                    End Function

                    Sub Test()
                        Dim x As C = CType([|M(GetType(C), Me)|], C)
                        Dim y As C = DirectCast([|M(GetType(C), Me)|], C)
                        Dim z As C = TryCast([|M(GetType(C), Me)|], C)
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class C
                    Function M(type as System.Type, x as Object) as Object
                        Return x
                    End Function
                
                    Function M(Of T)(x as T) as T
                        Return x
                    End Function

                    Sub Test()
                        Dim x As C = M(Me)
                        Dim y As C = M(Me)
                        Dim z As C = M(Me)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task NeededCastIsPreserved_OffersFixer_VB()
        {
            string source = """
                Class C
                    Function M(type as System.Type, x as Object) as Object
                        Return x
                    End Function

                    Function M(Of T)(x as T) as Object
                        Return x
                    End Function

                    Sub Test()
                        Dim x As C = CType([|M(GetType(C), Me)|], C)
                        Dim y As C = DirectCast([|M(GetType(C), Me)|], C)
                        Dim z As C = TryCast([|M(GetType(C), Me)|], C)
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class C
                    Function M(type as System.Type, x as Object) as Object
                        Return x
                    End Function
                
                    Function M(Of T)(x as T) as Object
                        Return x
                    End Function

                    Sub Test()
                        Dim x As C = CType(M(Me), C)
                        Dim y As C = DirectCast(M(Me), C)
                        Dim z As C = TryCast(M(Me), C)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task NamedParametersArePreserved_OffersFixer_VB()
        {
            string source = """
                Class C
                    Sub M(type as System.Type, x as Integer) : End Sub
                    Sub M(Of T)(x as Integer) : End Sub

                    Sub Test()
                        [|M(x:=0, type:=GetType(C))|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class C
                    Sub M(type as System.Type, x as Integer) : End Sub
                    Sub M(Of T)(x as Integer) : End Sub

                    Sub Test()
                        M(Of C)(x:=0)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task TriviaIsPreserved_OffersFixer_VB()
        {
            string source = """
                Class C
                    Sub M(type as System.Type, x as Integer) : End Sub
                    Sub M(Of T)(x as Integer) : End Sub

                    Sub Test()
                        ' reticulates the splines
                        [|M(GetType(C), 0)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class C
                    Sub M(type as System.Type, x as Integer) : End Sub
                    Sub M(Of T)(x as Integer) : End Sub

                    Sub Test()
                        ' reticulates the splines
                        M(Of C)(0)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task TriviaIsPreservedWhenCastIsRemoved_OffersFixer_VB()
        {
            string source = """
                Class C
                    Function M(type as System.Type, x as Object) as Object
                        Return x
                    End Function

                    Function M(Of T)(x as T) as T
                        Return x
                    End Function

                    Sub Test()
                        ' reticulates the splines
                        Dim x As C = CType([|M(GetType(C), Me)|], C)
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class C
                    Function M(type as System.Type, x as Object) as Object
                        Return x
                    End Function
                
                    Function M(Of T)(x as T) as T
                        Return x
                    End Function

                    Sub Test()
                        ' reticulates the splines
                        Dim x As C = M(Me)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }
    }
}
