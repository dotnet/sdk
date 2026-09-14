// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.UseStartsWithInsteadOfIndexOfComparisonWithZero,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpUseStartsWithInsteadOfIndexOfComparisonWithZeroCodeFix>;

using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.UseStartsWithInsteadOfIndexOfComparisonWithZero,
    Microsoft.NetCore.VisualBasic.Analyzers.Performance.BasicUseStartsWithInsteadOfIndexOfComparisonWithZeroCodeFix>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class UseStartsWithInsteadOfIndexOfComparisonWithZeroTests
    {
        private static async Task VerifyCodeFixVBAsync(string source, string fixedSource, ReferenceAssemblies referenceAssemblies)
        {
            await new VerifyVB.Test
            {
                TestCode = source,
                FixedCode = fixedSource,
                ReferenceAssemblies = referenceAssemblies,
            }.RunAsync();
        }

        private static async Task VerifyCodeFixCSAsync(string source, string fixedSource, ReferenceAssemblies referenceAssemblies)
        {
            await new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = fixedSource,
                ReferenceAssemblies = referenceAssemblies,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8,
            }.RunAsync();
        }

        [Fact]
        public async Task GreaterThanZero_CSharp_NoDiagnostic()
        {
            var testCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = a.IndexOf("") > 0;
                    }
                }
                """;

            await VerifyCodeFixCSAsync(testCode, testCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixCSAsync(testCode, testCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task GreaterThanZero_VB_Diagnostic()
        {
            var testCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = a.IndexOf("abc") > 0
                    End Sub
                End Class
                """;

            await VerifyCodeFixVBAsync(testCode, testCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixVBAsync(testCode, testCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task SimpleScenario_CSharp_Diagnostic()
        {
            var testCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = [|a.IndexOf("") == 0|];
                    }
                }
                """;

            var fixedCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = a.StartsWith("");
                    }
                }
                """;

            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task SimpleScenario_VB_Diagnostic()
        {
            var testCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = [|a.IndexOf("abc") = 0|]
                    End Sub
                End Class
                """;

            var fixedCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = a.StartsWith("abc")
                    End Sub
                End Class
                """;

            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task ZeroOnLeft_CSharp_Diagnostic()
        {
            var testCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = [|0 == a.IndexOf("")|];
                    }
                }
                """;

            var fixedCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = a.StartsWith("");
                    }
                }
                """;

            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task ZeroOnLeft_VB_Diagnostic()
        {
            var testCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = [|0 = a.IndexOf("abc")|]
                    End Sub
                End Class
                """;

            var fixedCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = a.StartsWith("abc")
                    End Sub
                End Class
                """;

            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task Negated_CSharp_Diagnostic()
        {
            var testCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = [|a.IndexOf("abc") != 0|];
                    }
                }
                """;

            var fixedCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = !a.StartsWith("abc");
                    }
                }
                """;

            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task Negated_VB_Diagnostic()
        {
            var testCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = [|a.IndexOf("abc") <> 0|]
                    End Sub
                End Class
                """;

            var fixedCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = Not a.StartsWith("abc")
                    End Sub
                End Class
                """;

            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task InArgument_CSharp_Diagnostic()
        {
            var testCode = """
                class C
                {
                    void M(string a)
                    {
                        System.Console.WriteLine([|a.IndexOf("abc") != 0|]);
                    }
                }
                """;

            var fixedCode = """
                class C
                {
                    void M(string a)
                    {
                        System.Console.WriteLine(!a.StartsWith("abc"));
                    }
                }
                """;

            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task InArgument_VB_Diagnostic()
        {
            var testCode = """
                Class C
                    Sub M(a As String)
                        System.Console.WriteLine([|a.IndexOf("abc") <> 0|])
                    End Sub
                End Class
                """;

            var fixedCode = """
                Class C
                    Sub M(a As String)
                        System.Console.WriteLine(Not a.StartsWith("abc"))
                    End Sub
                End Class
                """;

            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task FixAll_CSharp_Diagnostic()
        {
            var testCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = [|a.IndexOf("abc") != 0|];
                        _ = [|a.IndexOf("abcd") != 0|];
                    }
                }
                """;

            var fixedCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = !a.StartsWith("abc");
                        _ = !a.StartsWith("abcd");
                    }
                }
                """;

            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task FixAll_VB_Diagnostic()
        {
            var testCode = """
                Class C
                    Sub M(a As String)
                        Dim unused1 = [|a.IndexOf("abc") <> 0|]
                        Dim unused2 = [|a.IndexOf("abcd") <> 0|]
                    End Sub
                End Class
                """;

            var fixedCode = """
                Class C
                    Sub M(a As String)
                        Dim unused1 = Not a.StartsWith("abc")
                        Dim unused2 = Not a.StartsWith("abcd")
                    End Sub
                End Class
                """;

            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task FixAllNested_CSharp_Diagnostic()
        {
            var testCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = [|a.IndexOf(([|"abc2".IndexOf("abc3") == 0|]).ToString()) == 0|];
                    }
                }
                """;

            var fixedCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = a.StartsWith(("abc2".StartsWith("abc3")).ToString());
                    }
                }
                """;

            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task FixAllNested_VB_Diagnostic()
        {
            var testCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = [|a.IndexOf(([|"abc2".IndexOf("abc3") = 0|]).ToString()) = 0|]
                    End Sub
                End Class
                """;

            var fixedCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = a.StartsWith(("abc2".StartsWith("abc3")).ToString())
                    End Sub
                End Class
                """;

            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task StringStringComparison_CSharp_Diagnostic()
        {
            var testCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = [|a.IndexOf("abc", System.StringComparison.Ordinal) == 0|];
                    }
                }
                """;

            var fixedCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = a.StartsWith("abc", System.StringComparison.Ordinal);
                    }
                }
                """;

            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task StringStringComparison_VB_Diagnostic()
        {
            var testCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = [|a.IndexOf("abc", System.StringComparison.Ordinal) = 0|]
                    End Sub
                End Class
                """;

            var fixedCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = a.StartsWith("abc", System.StringComparison.Ordinal)
                    End Sub
                End Class
                """;

            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task Char_CSharp_Diagnostic()
        {
            var testCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = [|a.IndexOf('a') == 0|];
                    }
                }
                """;

            var fixedCode20 = """
                class C
                {
                    void M(string a)
                    {
                        _ = a.Length > 0 && a[0] == 'a';
                    }
                }
                """;

            var fixedCode21 = """
                class C
                {
                    void M(string a)
                    {
                        _ = a.StartsWith('a');
                    }
                }
                """;

            await VerifyCodeFixCSAsync(testCode, fixedCode20, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixCSAsync(testCode, fixedCode21, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task Char_VB_Diagnostic()
        {
            var testCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = [|a.IndexOf("a"c) = 0|]
                    End Sub
                End Class
                """;

            var fixedCode20 = """
                Class C
                    Sub M(a As String)
                        Dim unused = a.Length > 0 AndAlso a(0) = "a"c
                    End Sub
                End Class
                """;

            var fixedCode21 = """
                Class C
                    Sub M(a As String)
                        Dim unused = a.StartsWith("a"c)
                    End Sub
                End Class
                """;

            await VerifyCodeFixVBAsync(testCode, fixedCode20, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixVBAsync(testCode, fixedCode21, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task Char_Negation_CSharp_Diagnostic()
        {
            var testCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = [|a.IndexOf('a') != 0|];
                    }
                }
                """;

            var fixedCode20 = """
                class C
                {
                    void M(string a)
                    {
                        _ = a.Length == 0 || a[0] != 'a';
                    }
                }
                """;

            var fixedCode21 = """
                class C
                {
                    void M(string a)
                    {
                        _ = !a.StartsWith('a');
                    }
                }
                """;

            await VerifyCodeFixCSAsync(testCode, fixedCode20, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixCSAsync(testCode, fixedCode21, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task Char_Negation_VB_Diagnostic()
        {
            var testCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = [|a.IndexOf("a"c) <> 0|]
                    End Sub
                End Class
                """;

            var fixedCode20 = """
                Class C
                    Sub M(a As String)
                        Dim unused = a.Length = 0 OrElse a(0) <> "a"c
                    End Sub
                End Class
                """;

            var fixedCode21 = """
                Class C
                    Sub M(a As String)
                        Dim unused = Not a.StartsWith("a"c)
                    End Sub
                End Class
                """;

            await VerifyCodeFixVBAsync(testCode, fixedCode20, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixVBAsync(testCode, fixedCode21, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task CharStringComparison_HardCodedChar_CSharp_Diagnostic()
        {
            var testCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = [|a.IndexOf('a', System.StringComparison.Ordinal) == 0|];
                    }
                }
                """;

            var fixedCode = """
                using System;

                class C
                {
                    void M(string a)
                    {
                        _ = a.AsSpan().StartsWith(stackalloc char[1] {
                            'a'
                        }, System.StringComparison.Ordinal);
                    }
                }
                """;

            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task CharStringComparison_HardCodedChar_VB_Diagnostic()
        {
            var testCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = [|a.IndexOf("a"c, System.StringComparison.Ordinal) = 0|]
                    End Sub
                End Class
                """;

            var fixedCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = a.StartsWith("a", System.StringComparison.Ordinal)
                    End Sub
                End Class
                """;

            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task CharStringComparison_Expression_CSharp_Diagnostic()
        {
            var testCode = """
                class C
                {
                    void M(string a, char exp)
                    {
                        _ = [|a.IndexOf(exp, System.StringComparison.Ordinal) == 0|];
                    }
                }
                """;

            var fixedCode = """
                using System;

                class C
                {
                    void M(string a, char exp)
                    {
                        _ = a.AsSpan().StartsWith(stackalloc char[1] {
                            exp
                        }, System.StringComparison.Ordinal);
                    }
                }
                """;

            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task CharStringComparison_Expression_VB_Diagnostic()
        {
            var testCode = """
                Class C
                    Sub M(a As String, exp As Char)
                        Dim unused = [|a.IndexOf(exp, System.StringComparison.Ordinal) = 0|]
                    End Sub
                End Class
                """;

            var fixedCode = """
                Class C
                    Sub M(a As String, exp As Char)
                        Dim unused = a.StartsWith(exp.ToString(), System.StringComparison.Ordinal)
                    End Sub
                End Class
                """;

            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task CharStringComparison_HardCodedChar_OutOfOrder_CSharp_Diagnostic()
        {
            var testCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = [|a.IndexOf(comparisonType: System.StringComparison.Ordinal, value: 'a') == 0|];
                    }
                }
                """;

            var fixedCode = """
                using System;

                class C
                {
                    void M(string a)
                    {
                        _ = a.AsSpan().StartsWith(comparisonType: System.StringComparison.Ordinal, value: stackalloc char[1] {
                            'a'
                        });
                    }
                }
                """;

            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task CharStringComparison_HardCodedChar_OutOfOrder_VB_Diagnostic()
        {
            var testCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = [|a.IndexOf(comparisonType:=System.StringComparison.Ordinal, value:="a"c) = 0|]
                    End Sub
                End Class
                """;

            var fixedCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = a.StartsWith(value:="a", comparisonType:=System.StringComparison.Ordinal)
                    End Sub
                End Class
                """;

            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task CharStringComparison_Expression_OutOfOrder_CSharp_Diagnostic()
        {
            var testCode = """
                class C
                {
                    void M(string a, char exp)
                    {
                        _ = [|a.IndexOf(comparisonType: System.StringComparison.Ordinal, value: exp) == 0|];
                    }
                }
                """;

            var fixedCode = """
                using System;

                class C
                {
                    void M(string a, char exp)
                    {
                        _ = a.AsSpan().StartsWith(comparisonType: System.StringComparison.Ordinal, value: stackalloc char[1] {
                            exp
                        });
                    }
                }
                """;

            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task CharStringComparison_Expression_OutOfOrder_VB_Diagnostic()
        {
            var testCode = """
                Class C
                    Sub M(a As String, exp As Char)
                        Dim unused = [|a.IndexOf(comparisonType:=System.StringComparison.Ordinal, value:=exp) = 0|]
                    End Sub
                End Class
                """;

            var fixedCode = """
                Class C
                    Sub M(a As String, exp As Char)
                        Dim unused = a.StartsWith(value:=exp.ToString(), comparisonType:=System.StringComparison.Ordinal)
                    End Sub
                End Class
                """;

            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task OutOfOrderNamedArguments_CSharp_Diagnostic()
        {
            var testCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = [|a.IndexOf(comparisonType: System.StringComparison.Ordinal, value: "abc") == 0|];
                    }
                }
                """;

            var fixedCode = """
                class C
                {
                    void M(string a)
                    {
                        _ = a.StartsWith(comparisonType: System.StringComparison.Ordinal, value: "abc");
                    }
                }
                """;

            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixCSAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [Fact]
        public async Task OutOfOrderNamedArguments_VB_Diagnostic()
        {
            // IInvocationOperation.Arguments appears to behave differently in C# vs VB.
            // In C#, the order of arguments are preserved, as they appear in source.
            // In VB, the order of arguments is the same as parameters order.
            // If we wanted to make VB behavior similar to OutOfOrderNamedArguments_CSharp_Diagnostic, we will need
            // to go back to syntax. This scenario doesn't seem important/common, so might be good for now until
            // we hear any user feedback.
            var testCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = [|a.IndexOf(comparisonType:=System.StringComparison.Ordinal, value:="abc") = 0|]
                    End Sub
                End Class
                """;

            var fixedCode = """
                Class C
                    Sub M(a As String)
                        Dim unused = a.StartsWith(value:="abc", comparisonType:=System.StringComparison.Ordinal)
                    End Sub
                End Class
                """;

            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard20);
            await VerifyCodeFixVBAsync(testCode, fixedCode, ReferenceAssemblies.NetStandard.NetStandard21);
        }
    }
}
