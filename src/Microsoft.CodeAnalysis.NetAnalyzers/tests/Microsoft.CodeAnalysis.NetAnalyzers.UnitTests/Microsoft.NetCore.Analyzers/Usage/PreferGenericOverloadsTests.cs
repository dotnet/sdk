// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Usage.CSharpPreferGenericOverloadsAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Usage.CSharpPreferGenericOverloadsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Usage.BasicPreferGenericOverloadsAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Usage.BasicPreferGenericOverloadsFixer>;

namespace Microsoft.NetCore.Analyzers.Usage.UnitTests
{
    [TestClass]
    public class PreferGenericOverloadsTests
    {
        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod, WorkItem(7246, "https://github.com/dotnet/roslyn-analyzers/issues/7246")]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod, WorkItem(7245, "https://github.com/dotnet/roslyn-analyzers/issues/7245")]
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

            await test.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(7245, "https://github.com/dotnet/roslyn-analyzers/issues/7245")]
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

            await test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
        [DataRow("params string[] values")]
        [DataRow("string value = \"different\"")]
        [DataRow("string value = null")]
        public async Task OmittedDefaultValue_NoDiagnostic_CS(string parameter)
        {
            string source = $$"""
                class C
                {
                    void M(System.Type type, string value = "fallback") {}
                    void M<T>({{parameter}}) {}
                    void Test() => M(typeof(C));
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        [DataRow("float", "0.0f", "-0.0f")]
        [DataRow("double", "0.0", "-0.0")]
        [DataRow("decimal", "1.0m", "1.00m")]
        public async Task OmittedDefaultRepresentation_NoDiagnostic_CS(string type, string originalDefault, string candidateDefault)
        {
            string source = $$"""
                class C
                {
                    void M(System.Type type, {{type}} value = {{originalDefault}}) {}
                    void M<T>({{type}} value = {{candidateDefault}}) {}
                    void Test() => M(typeof(C));
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        [DataRow("\"fallback\"", "\"fallback\"", "")]
        [DataRow("null", "null", "")]
        [DataRow("\"fallback\"", "\"different\"", ", \"supplied\"")]
        public async Task OptionalDefaultValueIsPreserved_OffersFixer_CS(string originalDefault, string defaultValue, string argument)
        {
            string source = $$"""
                class C
                {
                    void M(System.Type type, string value = {{originalDefault}}) {}
                    void M<T>(string value = {{defaultValue}}) {}
                    void Test() => [|M(typeof(C){{argument}})|];
                }
                """;
            string fixedSource = $$"""
                class C
                {
                    void M(System.Type type, string value = {{originalDefault}}) {}
                    void M<T>(string value = {{defaultValue}}) {}
                    void Test() => M<C>({{argument.TrimStart(',', ' ')}});
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
        public async Task SeparatorTriviaIsPreserved_OffersFixer_CS()
        {
            string source = """
                class C
                {
                    void M(System.Type type, params string[] args) {}
                    void M<T>(params string[] args) {}
                    void Test()
                    {
                        [|M(typeof(C), "hello", // explanation
                            "world")|];
                    }
                }
                """;
            string fixedSource = """
                class C
                {
                    void M(System.Type type, params string[] args) {}
                    void M<T>(params string[] args) {}
                    void Test()
                    {
                        M<C>("hello", // explanation
                            "world");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
        [DataRow("/* selector */ typeof(C), \"hello\"", "/* selector */  \"hello\"")]
        [DataRow("value: \"hello\", type: typeof(C) /* selector */", "value: \"hello\"  /* selector */")]
        public async Task SelectorTriviaIsPreserved_OffersFixer_CS(string arguments, string remainingArguments)
        {
            string source = $$"""
                class C
                {
                    void M(System.Type type, string value) {}
                    void M<T>(string value) {}
                    void Test() => [|M({{arguments}})|];
                }
                """;
            string fixedSource = $$"""
                class C
                {
                    void M(System.Type type, string value) {}
                    void M<T>(string value) {}
                    void Test() => M<C>({{remainingArguments}});
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod, WorkItem(52654, "https://github.com/dotnet/sdk/issues/52654")]
        public async Task TypeOfPassedToGenericTypeParameter_NoDiagnostic_CS()
        {
            string source = """
                class C
                {
                    void Test()
                    {
                        System.Collections.Immutable.ImmutableHashSet<System.Type> x = System.Collections.Immutable.ImmutableHashSet.Create(typeof(System.Type));
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [TestMethod, WorkItem(53189, "https://github.com/dotnet/sdk/issues/53189")]
        public async Task ExpandedParamsArgumentsArePreserved_OffersFixer_CS()
        {
            string source = """
                class MyClass {}

                class Activator
                {
                    public T Create<T>(params string[] args) => default;
                    public object Create(System.Type type, params string[] args) => default;
                }

                class C
                {
                    void Test()
                    {
                        var activator = new Activator();
                        object value = [|activator.Create(typeof(MyClass), "hello", "world")|];
                    }
                }
                """;

            string fixedSource = """
                class MyClass {}

                class Activator
                {
                    public T Create<T>(params string[] args) => default;
                    public object Create(System.Type type, params string[] args) => default;
                }

                class C
                {
                    void Test()
                    {
                        var activator = new Activator();
                        object value = activator.Create<MyClass>("hello", "world");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
        [DataRow("object", "object", "\"hello\"")]
        [DataRow("object[]", "object[]", "new object[] { \"hello\" }")]
        public async Task ExpandedParamsToNonParams_NoDiagnostic_CS(string elementType, string parameterType, string arguments)
        {
            string source = $$"""
                class C
                {
                    static object M(System.Type type, params {{elementType}}[] args) => M<object>(args);
                    static object M<T>({{parameterType}} args) => args;
                    static object Test() => M(typeof(C), {{arguments}});
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(", \"hello\"")]
        [DataRow(", \"hello\", \"world\"")]
        public async Task ExpandedParamsWithDifferentArrayType_NoDiagnostic_CS(string arguments)
        {
            string source = $$"""
                class C
                {
                    static object M(System.Type type, params string[] args) => M<object>(args);
                    static object M<T>(params object[] args) => args;
                    static object Test() => M(typeof(C){{arguments}});
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        [DataRow("object", "new object[] { \"hello\" }")]
        [DataRow("object[]", "new object[] { \"hello\" }")]
        [DataRow("params object[]", "new object[] { \"hello\" }")]
        [DataRow("params object[]", "new string[] { \"hello\" }")]
        public async Task ExplicitParamsArrayIsPreserved_OffersFixer_CS(string parameterType, string argument)
        {
            string source = $$"""
                class C
                {
                    static object M(System.Type type, params object[] args) => M<object>(args);
                    static object M<T>({{parameterType}} args) => args;
                    static object Test() => [|M(typeof(C), {{argument}})|];
                }
                """;
            string fixedSource = $$"""
                class C
                {
                    static object M(System.Type type, params object[] args) => M<object>(args);
                    static object M<T>({{parameterType}} args) => args;
                    static object Test() => M<C>({{argument}});
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(", \"hello\"")]
        [DataRow(", \"hello\", \"world\"")]
        [DataRow(", (object)new string[] { \"hello\" }")]
        public async Task ExpandedParamsWithSameArrayType_OffersFixer_CS(string arguments)
        {
            string source = $$"""
                class C
                {
                    static object M(int prefix, System.Type type, params object[] args) => M<object>(prefix, args);
                    static object M<T>(int prefix, params object[] args) => args;
                    static object Test() => [|M(0, typeof(C){{arguments}})|];
                }
                """;
            string fixedSource = $$"""
                class C
                {
                    static object M(int prefix, System.Type type, params object[] args) => M<object>(prefix, args);
                    static object M<T>(int prefix, params object[] args) => args;
                    static object Test() => M<C>(0{{arguments}});
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
        [DataRow("", "", "")]
        [DataRow(", string first", "", ", \"hello\"")]
        [DataRow(", string first, string second", "", ", \"hello\", \"world\"")]
        [DataRow(", int prefix", "int prefix, ", ", 0")]
        [DataRow(", int prefix, string first", "int prefix, ", ", 0, \"hello\"")]
        [DataRow(", int prefix, string first, string second", "int prefix, ", ", 0, \"hello\", \"world\"")]
        [DataRow(", string[] args", "", ", new string[] { \"hello\", \"world\" }")]
        public async Task FixedArgumentsToGenericParams_OffersFixer_CS(string parameters, string prefix, string arguments)
        {
            string source = $$"""
                class C
                {
                    void M(System.Type type{{parameters}}) {}
                    void M<T>({{prefix}}params string[] args) {}
                    void Test()
                    {
                        [|M(typeof(C){{arguments}})|];
                    }
                }
                """;
            string fixedSource = $$"""
                class C
                {
                    void M(System.Type type{{parameters}}) {}
                    void M<T>({{prefix}}params string[] args) {}
                    void Test()
                    {
                        M<C>({{arguments.TrimStart(',', ' ')}});
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
        [DataRow(", int value", "", ", 1")]
        [DataRow(", string first, int second", "", ", \"hello\", 1")]
        [DataRow("", "int prefix, ", "")]
        [DataRow(", int prefix", "int prefix, int required, ", ", 0")]
        public async Task FixedArgumentsToGenericParams_NoDiagnostic_CS(string parameters, string prefix, string arguments)
        {
            string source = $$"""
                class C
                {
                    void M(System.Type type{{parameters}}) {}
                    void M<T>({{prefix}}params string[] args) {}
                    void Test()
                    {
                        M(typeof(C){{arguments}});
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        public async Task GenericParamsCompetingOverloadHasIncompatibleReturn_NoDiagnostic_CS()
        {
            string source = """
                class C
                {
                    string M(System.Type type, string value) => "";
                    string M<T>(params string[] args) => "";
                    object M<T>(string value) => null;
                    string Test() => M(typeof(C), "hello");
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        public async Task IncompatibleReturnTypeWithSelector_OffersFixerForExpressionStatement_CS()
        {
            string source = """
                class C
                {
                    int M(System.Type type) => 0;
                    string M<T>() => "";
                    void Test()
                    {
                        [|M(typeof(C))|];
                    }
                }
                """;
            string fixedSource = """
                class C
                {
                    int M(System.Type type) => 0;
                    string M<T>() => "";
                    void Test()
                    {
                        M<C>();
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
        public async Task IncompatibleReturnTypeWithSelector_NoDiagnosticForConsumedResult_CS()
        {
            string source = """
                class C
                {
                    int M(System.Type type) => 0;
                    string M<T>() => "";
                    int Test() => M(typeof(C));
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        public async Task GenericTypeParameterInExpressionStatement_NoDiagnostic_CS()
        {
            string source = """
                class C
                {
                    void Test()
                    {
                        System.Collections.Immutable.ImmutableHashSet.Create(typeof(C));
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

            await test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
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

            await test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod, WorkItem(7246, "https://github.com/dotnet/roslyn-analyzers/issues/7246")]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
        [DataRow("ParamArray values As String()")]
        [DataRow("Optional value As String = \"different\"")]
        [DataRow("Optional value As String = Nothing")]
        public async Task OmittedDefaultValue_NoDiagnostic_VB(string parameter)
        {
            string source = $$"""
                Class C
                    Sub M(type As System.Type, Optional value As String = "fallback") : End Sub
                    Sub M(Of T)({{parameter}}) : End Sub
                    Sub Test()
                        M(GetType(C))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        [DataRow("Single", "0.0F", "-0.0F")]
        [DataRow("Double", "0.0R", "-0.0R")]
        [DataRow("Decimal", "1.0D", "1.00D")]
        public async Task OmittedDefaultRepresentation_NoDiagnostic_VB(string type, string originalDefault, string candidateDefault)
        {
            string source = $$"""
                Class C
                    Sub M(type As System.Type, Optional value As {{type}} = {{originalDefault}}) : End Sub
                    Sub M(Of T)(Optional value As {{type}} = {{candidateDefault}}) : End Sub
                    Sub Test()
                        M(GetType(C))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        [DataRow("\"fallback\"", "\"fallback\"", "")]
        [DataRow("Nothing", "Nothing", "")]
        [DataRow("\"fallback\"", "\"different\"", ", \"supplied\"")]
        public async Task OptionalDefaultValueIsPreserved_OffersFixer_VB(string originalDefault, string defaultValue, string argument)
        {
            string source = $$"""
                Class C
                    Sub M(type As System.Type, Optional value As String = {{originalDefault}}) : End Sub
                    Sub M(Of T)(Optional value As String = {{defaultValue}}) : End Sub
                    Sub Test()
                        [|M(GetType(C){{argument}})|]
                    End Sub
                End Class
                """;
            string fixedSource = $$"""
                Class C
                    Sub M(type As System.Type, Optional value As String = {{originalDefault}}) : End Sub
                    Sub M(Of T)(Optional value As String = {{defaultValue}}) : End Sub
                    Sub Test()
                        M(Of C)({{argument.TrimStart(',', ' ')}})
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
        public async Task SeparatorTriviaIsPreserved_OffersFixer_VB()
        {
            string source = """
                Class C
                    Sub M(type As System.Type, ParamArray args As String()) : End Sub
                    Sub M(Of T)(ParamArray args As String()) : End Sub
                    Sub Test()
                        [|M(GetType(C), "hello", ' explanation
                            "world")|]
                    End Sub
                End Class
                """;
            string fixedSource = """
                Class C
                    Sub M(type As System.Type, ParamArray args As String()) : End Sub
                    Sub M(Of T)(ParamArray args As String()) : End Sub
                    Sub Test()
                        M(Of C)("hello", ' explanation
                            "world")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
        public async Task SelectorTriviaIsPreserved_OffersFixer_VB()
        {
            string source = """
                Class C
                    Sub M(type As System.Type, value As String) : End Sub
                    Sub M(Of T)(value As String) : End Sub
                    Sub Test()
                        [|M(GetType(C), ' selector
                            "hello")|]
                    End Sub
                End Class
                """;
            string fixedSource = """
                Class C
                    Sub M(type As System.Type, value As String) : End Sub
                    Sub M(Of T)(value As String) : End Sub
                    Sub Test()
                        M(Of C)( ' selector
                            "hello")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod, WorkItem(52654, "https://github.com/dotnet/sdk/issues/52654")]
        public async Task GetTypePassedToGenericTypeParameter_NoDiagnostic_VB()
        {
            string source = """
                Class C
                    Sub Test()
                        Dim x As System.Collections.Immutable.ImmutableHashSet(Of System.Type) = System.Collections.Immutable.ImmutableHashSet.Create(GetType(System.Type))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [TestMethod, WorkItem(53189, "https://github.com/dotnet/sdk/issues/53189")]
        public async Task ExpandedParamArrayArgumentsArePreserved_OffersFixer_VB()
        {
            string source = """
                Class SampleClass
                End Class

                Class Factory
                    Public Function Create(Of T)(ParamArray args As String()) As T
                        Return Nothing
                    End Function

                    Public Function Create(type As System.Type, ParamArray args As String()) As Object
                        Return Nothing
                    End Function
                End Class

                Class C
                    Sub Test()
                        Dim factory = New Factory()
                        Dim value As Object = [|factory.Create(GetType(SampleClass), "hello", "world")|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Class SampleClass
                End Class

                Class Factory
                    Public Function Create(Of T)(ParamArray args As String()) As T
                        Return Nothing
                    End Function

                    Public Function Create(type As System.Type, ParamArray args As String()) As Object
                        Return Nothing
                    End Function
                End Class

                Class C
                    Sub Test()
                        Dim factory = New Factory()
                        Dim value As Object = factory.Create(Of SampleClass)("hello", "world")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
        [DataRow("Object", "Object", "\"hello\"")]
        [DataRow("Object()", "Object()", "New Object() { \"hello\" }")]
        public async Task ExpandedParamsToNonParams_NoDiagnostic_VB(string elementType, string parameterType, string arguments)
        {
            string source = $$"""
                Class C
                    Shared Function M(type As System.Type, ParamArray args As {{elementType}}()) As Object
                        Return M(Of Object)(args)
                    End Function
                    Shared Function M(Of T)(args As {{parameterType}}) As Object
                        Return args
                    End Function
                    Shared Function Test() As Object
                        Return M(GetType(C), {{arguments}})
                    End Function
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(", \"hello\"")]
        [DataRow(", \"hello\", \"world\"")]
        public async Task ExpandedParamsWithDifferentArrayType_NoDiagnostic_VB(string arguments)
        {
            string source = $$"""
                Class C
                    Shared Function M(type As System.Type, ParamArray args As String()) As Object
                        Return M(Of Object)(args)
                    End Function
                    Shared Function M(Of T)(ParamArray args As Object()) As Object
                        Return args
                    End Function
                    Shared Function Test() As Object
                        Return M(GetType(C){{arguments}})
                    End Function
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        [DataRow("args As Object", "New Object() { \"hello\" }")]
        [DataRow("args As Object()", "New Object() { \"hello\" }")]
        [DataRow("ParamArray args As Object()", "New Object() { \"hello\" }")]
        [DataRow("ParamArray args As Object()", "New String() { \"hello\" }")]
        public async Task ExplicitParamsArrayIsPreserved_OffersFixer_VB(string parameter, string argument)
        {
            string source = $$"""
                Class C
                    Shared Function M(type As System.Type, ParamArray args As Object()) As Object
                        Return M(Of Object)(args)
                    End Function
                    Shared Function M(Of T)({{parameter}}) As Object
                        Return args
                    End Function
                    Shared Function Test() As Object
                        Return [|M(GetType(C), {{argument}})|]
                    End Function
                End Class
                """;
            string fixedSource = $$"""
                Class C
                    Shared Function M(type As System.Type, ParamArray args As Object()) As Object
                        Return M(Of Object)(args)
                    End Function
                    Shared Function M(Of T)({{parameter}}) As Object
                        Return args
                    End Function
                    Shared Function Test() As Object
                        Return M(Of C)({{argument}})
                    End Function
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(", \"hello\"")]
        [DataRow(", \"hello\", \"world\"")]
        [DataRow(", DirectCast(New String() { \"hello\" }, Object)")]
        public async Task ExpandedParamsWithSameArrayType_OffersFixer_VB(string arguments)
        {
            string source = $$"""
                Class C
                    Shared Function M(prefix As Integer, type As System.Type, ParamArray args As Object()) As Object
                        Return M(Of Object)(prefix, args)
                    End Function
                    Shared Function M(Of T)(prefix As Integer, ParamArray args As Object()) As Object
                        Return args
                    End Function
                    Shared Function Test() As Object
                        Return [|M(0, GetType(C){{arguments}})|]
                    End Function
                End Class
                """;
            string fixedSource = $$"""
                Class C
                    Shared Function M(prefix As Integer, type As System.Type, ParamArray args As Object()) As Object
                        Return M(Of Object)(prefix, args)
                    End Function
                    Shared Function M(Of T)(prefix As Integer, ParamArray args As Object()) As Object
                        Return args
                    End Function
                    Shared Function Test() As Object
                        Return M(Of C)(0{{arguments}})
                    End Function
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
        [DataRow("", "", "")]
        [DataRow(", first As String", "", ", \"hello\"")]
        [DataRow(", first As String, second As String", "", ", \"hello\", \"world\"")]
        [DataRow(", prefix As Integer", "prefix As Integer, ", ", 0")]
        [DataRow(", prefix As Integer, first As String", "prefix As Integer, ", ", 0, \"hello\"")]
        [DataRow(", prefix As Integer, first As String, second As String", "prefix As Integer, ", ", 0, \"hello\", \"world\"")]
        [DataRow(", args As String()", "", ", New String() { \"hello\", \"world\" }")]
        public async Task FixedArgumentsToGenericParams_OffersFixer_VB(string parameters, string prefix, string arguments)
        {
            string source = $$"""
                Class C
                    Sub M(type As System.Type{{parameters}}) : End Sub
                    Sub M(Of T)({{prefix}}ParamArray args As String()) : End Sub
                    Sub Test()
                        [|M(GetType(C){{arguments}})|]
                    End Sub
                End Class
                """;
            string fixedSource = $$"""
                Class C
                    Sub M(type As System.Type{{parameters}}) : End Sub
                    Sub M(Of T)({{prefix}}ParamArray args As String()) : End Sub
                    Sub Test()
                        M(Of C)({{arguments.TrimStart(',', ' ')}})
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
        [DataRow(", value As Integer", "", ", 1")]
        [DataRow(", first As String, second As Integer", "", ", \"hello\", 1")]
        [DataRow("", "prefix As Integer, ", "")]
        [DataRow(", prefix As Integer", "prefix As Integer, required As Integer, ", ", 0")]
        public async Task FixedArgumentsToGenericParams_NoDiagnostic_VB(string parameters, string prefix, string arguments)
        {
            string source = $$"""
                Class C
                    Sub M(type As System.Type{{parameters}}) : End Sub
                    Sub M(Of T)({{prefix}}ParamArray args As String()) : End Sub
                    Sub Test()
                        M(GetType(C){{arguments}})
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        public async Task GenericParamsCompetingOverloadHasIncompatibleReturn_NoDiagnostic_VB()
        {
            string source = """
                Class C
                    Function M(type As System.Type, value As String) As String
                        Return ""
                    End Function
                    Function M(Of T)(ParamArray args As String()) As String
                        Return ""
                    End Function
                    Function M(Of T)(value As String) As Object
                        Return Nothing
                    End Function
                    Function Test() As String
                        Return M(GetType(C), "hello")
                    End Function
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        public async Task IncompatibleReturnTypeWithSelector_OffersFixerForExpressionStatement_VB()
        {
            string source = """
                Class C
                    Function M(type As System.Type) As Integer
                        Return 0
                    End Function
                    Function M(Of T)() As String
                        Return ""
                    End Function
                    Sub Test()
                        [|M(GetType(C))|]
                    End Sub
                End Class
                """;
            string fixedSource = """
                Class C
                    Function M(type As System.Type) As Integer
                        Return 0
                    End Function
                    Function M(Of T)() As String
                        Return ""
                    End Function
                    Sub Test()
                        M(Of C)()
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
        public async Task IncompatibleReturnTypeWithSelector_NoDiagnosticForConsumedResult_VB()
        {
            string source = """
                Class C
                    Function M(type As System.Type) As Integer
                        Return 0
                    End Function
                    Function M(Of T)() As String
                        Return ""
                    End Function
                    Function Test() As Integer
                        Return M(GetType(C))
                    End Function
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        public async Task GenericTypeParameterInExpressionStatement_NoDiagnostic_VB()
        {
            string source = """
                Class C
                    Sub Test()
                        System.Collections.Immutable.ImmutableHashSet.Create(GetType(C))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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
