// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.Maintainability.AvoidLengthCalculationWhenSlicingToEndAnalyzer,
    Microsoft.CodeQuality.Analyzers.Maintainability.AvoidLengthCalculationWhenSlicingToEndFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.Maintainability.AvoidLengthCalculationWhenSlicingToEndAnalyzer,
    Microsoft.CodeQuality.Analyzers.Maintainability.AvoidLengthCalculationWhenSlicingToEndFixer>;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.UnitTests
{
    public class AvoidLengthCalculationWhenSlicingToEndTests
    {
        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task ConstantLiteralSubtracted_OffersFixer_CS(string type)
        {
            string source = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        [|x.{{TestMethodFromType(type)}}(5, x.Length - 5)|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(5);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task ConstantLiteralsSumEqualSubtracted_OffersFixer_CS(string type)
        {
            string source = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        [|x.{{TestMethodFromType(type)}}(6, x.Length - 1 - 2 - 3)|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(6);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task ConstantSubtracted_OffersFixer_CS(string type)
        {
            string source = $$"""
                class C
                {
                    const int Constant = 2;

                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        [|x.{{TestMethodFromType(type)}}(Constant, x.Length - Constant)|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    const int Constant = 2;

                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(Constant);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task FieldSubtracted_OffersFixer_CS(string type)
        {
            string source = $$"""
                class C
                {
                    private int Field = 3;

                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        [|x.{{TestMethodFromType(type)}}(Field, x.Length - Field)|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    private int Field = 3;

                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(Field);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task FieldWithInstanceReferenceSubtracted_OffersFixer_CS(string type)
        {
            string source = $$"""
                class C
                {
                    private int Field = 3;

                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        [|x.{{TestMethodFromType(type)}}(this.Field, x.Length - this.Field)|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    private int Field = 3;

                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(this.Field);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task LocalSubtracted_OffersFixer_CS(string type)
        {
            string source = $$"""
                class C
                {
                    void M()
                    {
                        int local = 2;
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        [|x.{{TestMethodFromType(type)}}(local, x.Length - local)|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    void M()
                    {
                        int local = 2;
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(local);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task ParameterSubtracted_OffersFixer_CS(string type)
        {
            string source = $$"""
                class C
                {
                    void M(int parameter)
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        [|x.{{TestMethodFromType(type)}}(parameter, x.Length - parameter)|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    void M(int parameter)
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(parameter);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task AutoPropertySubtracted_OffersFixer_CS(string type)
        {
            string source = $$"""
                class C
                {
                    public int AutoProperty { get; }

                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        [|x.{{TestMethodFromType(type)}}(AutoProperty, x.Length - AutoProperty)|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    public int AutoProperty { get; }

                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(AutoProperty);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task AutoPropertyWithInstanceReferenceSubtracted_OffersFixer_CS(string type)
        {
            string source = $$"""
                class C
                {
                    public int AutoProperty { get; }

                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        [|x.{{TestMethodFromType(type)}}(this.AutoProperty, x.Length - this.AutoProperty)|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    public int AutoProperty { get; }

                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(this.AutoProperty);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task ExplicitPropertySubtracted_NoDiagnostic_CS(string type)
        {
            string source = $$"""
                class C
                {
                    private readonly int Field;

                    public int Property
                    {
                        get { return Field; }
                    }

                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(Property, x.Length - Property);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task MethodSubtracted_NoDiagnostic_CS(string type)
        {
            string source = $$"""
                class C
                {
                    int Method()
                    {
                        return 1;
                    }

                    void M(int parameter)
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(Method(), x.Length - Method());
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task SupportedUnaryOperator_OffersFixer_CS(string type)
        {
            string source = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        [|x.{{TestMethodFromType(type)}}(+5, x.Length + -5)|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(+5);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task UnsupportedUnaryOperator_NoDiagnostic_CS(string type)
        {
            string source = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(~1, x.Length - ~1);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task SupportedBinaryOperator_OffersFixer_CS(string type)
        {
            string source = $$"""
                class C
                {
                    private int Field;

                    void M()
                    {
                        int local1 = 1;
                        int local2 = 2;

                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        [|x.{{TestMethodFromType(type)}}(local1, x.Length - local1)|];
                        [|x.{{TestMethodFromType(type)}}(local1 + local2, x.Length - local1 - local2)|];
                        [|x.{{TestMethodFromType(type)}}(local1 + local2, x.Length - (local1 + local2))|];
                        [|x.{{TestMethodFromType(type)}}(Field, x.Length - Field)|];
                        [|x.{{TestMethodFromType(type)}}(Field + 5, x.Length - Field - 5)|];
                        [|x.{{TestMethodFromType(type)}}(Field + 5, x.Length - (Field + 5))|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    private int Field;
                
                    void M()
                    {
                        int local1 = 1;
                        int local2 = 2;
                
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(local1);
                        x.{{TestMethodFromType(type)}}(local1 + local2);
                        x.{{TestMethodFromType(type)}}(local1 + local2);
                        x.{{TestMethodFromType(type)}}(Field);
                        x.{{TestMethodFromType(type)}}(Field + 5);
                        x.{{TestMethodFromType(type)}}(Field + 5);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task UnsupportedBinaryOperator_NoDiagnostic_CS(string type)
        {
            string source = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(1 * 2, x.Length - 2);
                        x.{{TestMethodFromType(type)}}(6 / 3, x.Length - 2);
                        x.{{TestMethodFromType(type)}}(5 % 3, x.Length - 2);
                        x.{{TestMethodFromType(type)}}(1 << 1, x.Length - 2);
                        x.{{TestMethodFromType(type)}}(4 >> 1, x.Length - 2);
                        x.{{TestMethodFromType(type)}}(2 & 2, x.Length - 2);
                        x.{{TestMethodFromType(type)}}(2 | 0, x.Length - 2);
                        x.{{TestMethodFromType(type)}}(2 ^ 0, x.Length - 2);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task DifferentConstantLiteralSubtracted_NoDiagnostic_CS(string type)
        {
            string source = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(0, x.Length - 5);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task DifferentConstantSubtracted_NoDiagnostic_CS(string type)
        {
            string source = $$"""
                class C
                {
                    const int Constant1 = 1;
                    const int Constant2 = 2;

                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(Constant1, x.Length - Constant2);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task DifferentFieldSubtracted_NoDiagnostic_CS(string type)
        {
            string source = $$"""
                class C
                {
                    private int Field1 = 1;
                    private int Field2 = 2;

                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(Field1, x.Length - Field2);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task DifferentLocalSubtracted_NoDiagnostic_CS(string type)
        {
            string source = $$"""
                class C
                {
                    void M()
                    {
                        int Local1 = 1;
                        int Local2 = 2;
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(Local1, x.Length - Local2);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task DifferentParameterSubtracted_NoDiagnostic_CS(string type)
        {
            string source = $$"""
                class C
                {
                    void M(int parameter1, int parameter2)
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(parameter1, x.Length - parameter2);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task DifferentAutoPropertySubtracted_NoDiagnostic_CS(string type)
        {
            string source = $$"""
                class C
                {
                    public int AutoProperty1 { get; }
                    public int AutoProperty2 { get; }

                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(AutoProperty1, x.Length - AutoProperty2);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task DifferentLengthPropertySubtracted_NoDiagnostic_CS(string type)
        {
            string source = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(5, x.Length - System.Environment.NewLine.Length - 5);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task ZeroAsStartNothingSubtracted_OffersFixer_CS(string type)
        {
            string source = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        [|x.{{TestMethodFromType(type)}}(0, x.Length)|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(0);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task AdditionalParenthesis_OffersFixer_CS(string type)
        {
            string source = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        [|x.{{TestMethodFromType(type)}}(((1 + 2)) + 3, x.Length - 1 - 2 - 3)|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(((1 + 2)) + 3);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task NestedConstantsSubtracted_OffersFixer_CS(string type)
        {
            string source = $$"""
                class C
                {
                    void M()
                    {
                        int local1 = 1;
                        int local2 = 1;
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        [|x.{{TestMethodFromType(type)}}(1 + 2 - (3 + (4 - +5 - 6 + -7) + 8) - 9, x.Length - 1 - 2 + 3 + 4 - 5 - 6 - 7 + 8 + 9)|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    void M()
                    {
                        int local1 = 1;
                        int local2 = 1;
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(1 + 2 - (3 + (4 - +5 - 6 + -7) + 8) - 9);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task NamedArguments_OffersFixer_CS(string type)
        {
            string source = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        [|x.{{TestMethodFromType(type)}}(start: 1, length: x.Length - 1)|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(start: 1);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task NamedArgumentsSwapped_OffersFixer_CS(string type)
        {
            string source = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        [|x.{{TestMethodFromType(type)}}(length: x.Length - 1, start: 1)|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        x.{{TestMethodFromType(type)}}(start: 1);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("System.Span<char>")]
        [InlineData("System.ReadOnlySpan<char>")]
        [InlineData("System.Memory<char>")]
        public async Task TriviaIsPreserved_OffersFixer_CS(string type)
        {
            string source = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        // reticulates the splines
                        [|x.{{TestMethodFromType(type)}}(1, x.Length - 1)|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    void M()
                    {
                        {{type}} x = {{InstantiateExpressionFromType_CS(type)}};
                        // reticulates the splines
                        x.{{TestMethodFromType(type)}}(1);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task StringLiteralSubstring_OffersFixer_CS()
        {
            string source = $$"""
                class C
                {
                    void M()
                    {
                        [|"test".Substring(2, "test".Length - 2)|];
                    }
                }
                """;

            string fixedSource = $$"""
                class C
                {
                    void M()
                    {
                        "test".Substring(2);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task ConstantLiteralSubtracted_OffersFixer_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        [|x.{{TestMethodFromType(type)}}(5, x.Length - 5)|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(5)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task ConstantLiteralsSumEqualSubtracted_OffersFixer_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        [|x.{{TestMethodFromType(type)}}(6, x.Length - 1 - 2 - 3)|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(6)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task ConstantSubtracted_OffersFixer_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Const Constant as Integer = 2

                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        [|x.{{TestMethodFromType(type)}}(Constant, x.Length - Constant)|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Const Constant as Integer = 2

                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(Constant)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task FieldSubtracted_OffersFixer_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Private ReadOnly Field as Integer = 3

                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        [|x.{{TestMethodFromType(type)}}(Field, x.Length - Field)|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Private ReadOnly Field as Integer = 3

                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(Field)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task FieldWithInstanceReferenceSubtracted_OffersFixer_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Private ReadOnly Field as Integer = 3

                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        [|x.{{TestMethodFromType(type)}}(Me.Field, x.Length - Me.Field)|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Private ReadOnly Field as Integer = 3

                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(Me.Field)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task LocalSubtracted_OffersFixer_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Public Sub M()
                        Dim local as Integer = 2
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        [|x.{{TestMethodFromType(type)}}(local, x.Length - local)|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Public Sub M()
                        Dim local as Integer = 2
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(local)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task ParameterSubtracted_OffersFixer_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Public Sub M(parameter as Integer)
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        [|x.{{TestMethodFromType(type)}}(parameter, x.Length - parameter)|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Public Sub M(parameter as Integer)
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(parameter)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task AutoPropertySubtracted_OffersFixer_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Public Property AutoProperty As Integer

                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        [|x.{{TestMethodFromType(type)}}(AutoProperty, x.Length - AutoProperty)|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Public Property AutoProperty As Integer

                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(AutoProperty)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task AutoPropertyWithInstanceReferenceSubtracted_OffersFixer_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Public Property AutoProperty As Integer

                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        [|x.{{TestMethodFromType(type)}}(Me.AutoProperty, x.Length - Me.AutoProperty)|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Public Property AutoProperty As Integer

                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(Me.AutoProperty)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task ExplicitPropertySubtracted_NoDiagnostic_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Private ReadOnly Field as Integer

                    Public ReadOnly Property Prop As Integer
                        Get
                            Return Field
                        End Get
                    End Property

                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(Prop, x.Length - Prop)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task MethodSubtracted_NoDiagnostic_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Private ReadOnly Field as Integer

                    Public Function Method() as Integer
                        Return 1
                    End Function

                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(Method(), x.Length - Method())
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task SupportedUnaryOperator_OffersFixer_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        [|x.{{TestMethodFromType(type)}}(+5, x.Length + -5)|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(+5)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task SupportedBinaryOperator_OffersFixer_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Private ReadOnly Field as Integer

                    Public Sub M()
                        Dim local1 as Integer = 1
                        Dim local2 as Integer = 2

                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        [|x.{{TestMethodFromType(type)}}(local1, x.Length - local1)|]
                        [|x.{{TestMethodFromType(type)}}(local1 + local2, x.Length - local1 - local2)|]
                        [|x.{{TestMethodFromType(type)}}(local1 + local2, x.Length - (local1 + local2))|]
                        [|x.{{TestMethodFromType(type)}}(Field, x.Length - Field)|]
                        [|x.{{TestMethodFromType(type)}}(Field + 5, x.Length - Field - 5)|]
                        [|x.{{TestMethodFromType(type)}}(Field + 5, x.Length - (Field + 5))|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Private ReadOnly Field as Integer
                
                    Public Sub M()
                        Dim local1 as Integer = 1
                        Dim local2 as Integer = 2
                
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(local1)
                        x.{{TestMethodFromType(type)}}(local1 + local2)
                        x.{{TestMethodFromType(type)}}(local1 + local2)
                        x.{{TestMethodFromType(type)}}(Field)
                        x.{{TestMethodFromType(type)}}(Field + 5)
                        x.{{TestMethodFromType(type)}}(Field + 5)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task UnsupportedBinaryOperator_NoDiagnostic_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(1 * 2, x.Length - 2)
                        x.{{TestMethodFromType(type)}}(6 / 3, x.Length - 2)
                        x.{{TestMethodFromType(type)}}(6 \ 3, x.Length - 2)
                        x.{{TestMethodFromType(type)}}(5 Mod 3, x.Length - 2)
                        x.{{TestMethodFromType(type)}}(2 ^ 1, x.Length - 2)
                        x.{{TestMethodFromType(type)}}(1 << 1, x.Length - 2)
                        x.{{TestMethodFromType(type)}}(4 >> 1, x.Length - 2)
                        x.{{TestMethodFromType(type)}}(2 And 2, x.Length - 2)
                        x.{{TestMethodFromType(type)}}(2 Or 0, x.Length - 2)
                        x.{{TestMethodFromType(type)}}(2 Xor 0, x.Length - 2)
                        x.{{TestMethodFromType(type)}}(2 Like 0, x.Length - 2)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task DifferentConstantLiteralSubtracted_NoDiagnostic_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(0, x.Length - 5)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task DifferentConstantSubtracted_NoDiagnostic_VB(string type)
        {
            string source = $$"""
                Public Class C
                    const Constant1 as Integer = 1
                    const Constant2 as Integer = 2

                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(Constant1, x.Length - Constant2)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task DifferentFieldSubtracted_NoDiagnostic_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Private ReadOnly Field1 as Integer = 1
                    Private ReadOnly Field2 as Integer = 2

                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(Field1, x.Length - Field2)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task DifferentLocalSubtracted_NoDiagnostic_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Public Sub M()
                        Dim local1 as Integer = 1
                        Dim local2 as Integer = 2

                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(local1, x.Length - local2)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task DifferentParameterSubtracted_NoDiagnostic_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Public Sub M(parameter1 as Integer, parameter2 as Integer)
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(parameter1, x.Length - parameter2)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task DifferentAutoPropertySubtracted_NoDiagnostic_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Public Property AutoProperty1 As Integer
                    Public Property AutoProperty2 As Integer

                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(AutoProperty1, x.Length - AutoProperty2)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task DifferentLengthPropertySubtracted_NoDiagnostic_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(5, x.Length - System.Environment.NewLine.Length - 5)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task ZeroAsStartNothingSubtracted_OffersFixer_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        [|x.{{TestMethodFromType(type)}}(0, x.Length)|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(0)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task AdditionalParenthesis_OffersFixer_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        [|x.{{TestMethodFromType(type)}}(((1 + 2)) + 3, x.Length - 1 - 2 - 3)|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(((1 + 2)) + 3)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task NestedConstantsSubtracted_OffersFixer_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        [|x.{{TestMethodFromType(type)}}(1 + 2 - (3 + (4 - +5 - 6 + -7) + 8) - 9, x.Length - 1 - 2 + 3 + 4 - 5 - 6 - 7 + 8 + 9)|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        x.{{TestMethodFromType(type)}}(1 + 2 - (3 + (4 - +5 - 6 + -7) + 8) - 9)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task NamedArguments_OffersFixer_VB()
        {
            string source = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as System.Memory(Of Char) = System.Memory(Of Char).Empty
                        [|x.Slice(start:=1, length:=x.Length - 1)|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as System.Memory(Of Char) = System.Memory(Of Char).Empty
                        x.Slice(start:=1)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task NamedArgumentsSwapped_OffersFixer_VB()
        {
            string source = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as System.Memory(Of Char) = System.Memory(Of Char).Empty
                        [|x.Slice(length:=x.Length - 1, start:=1)|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as System.Memory(Of Char) = System.Memory(Of Char).Empty
                        x.Slice(start:=1)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.Memory<char>")]
        public async Task TriviaIsPreserved_OffersFixer_VB(string type)
        {
            string source = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        ' reticulates the splines
                        [|x.{{TestMethodFromType(type)}}(1, x.Length - 1)|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as {{TypeForVB(type)}} = {{InstantiateExpressionFromType_VB(type)}}
                        ' reticulates the splines
                        x.{{TestMethodFromType(type)}}(1)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task StringLiteralSubstring_OffersFixer_VB()
        {
            string source = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as String = [|"test".Substring(2, "test".Length - 2)|]
                    End Sub
                End Class
                """;

            string fixedSource = $$"""
                Public Class C
                    Public Sub M()
                        Dim x as String = "test".Substring(2)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        #region Helpers
        private static string InstantiateExpressionFromType_CS(string type)
        {
            return type switch
            {
                "string" => "string.Empty",
                "System.Span<char>" => "System.Span<char>.Empty",
                "System.ReadOnlySpan<char>" => "System.ReadOnlySpan<char>.Empty",
                "System.Memory<char>" => "System.Memory<char>.Empty",
                _ => throw new NotImplementedException()
            };
        }

        private static string InstantiateExpressionFromType_VB(string type)
        {
            return type switch
            {
                "string" => "String.Empty",
                "System.Memory<char>" => "System.Memory(Of Char).Empty",
                _ => throw new NotImplementedException()
            };
        }

        private static string TypeForVB(string type)
        {
            return type switch
            {
                "string" => "String",
                "System.Memory<char>" => "System.Memory(Of Char)",
                _ => throw new NotImplementedException()
            };
        }

        private static string TestMethodFromType(string type)
        {
            switch (type)
            {
                case "string":
                    return "Substring";
                default:
                    return "Slice";
            }
        }
        #endregion
    }
}
