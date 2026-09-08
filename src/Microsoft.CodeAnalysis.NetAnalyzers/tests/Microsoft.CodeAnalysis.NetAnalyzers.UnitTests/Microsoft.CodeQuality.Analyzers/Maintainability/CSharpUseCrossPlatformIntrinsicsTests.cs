// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.Maintainability.CSharpUseCrossPlatformIntrinsicsAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.Maintainability.CSharpUseCrossPlatformIntrinsicsFixer>;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.UnitTests
{
    using static UseCrossPlatformIntrinsicsAnalyzer;

    [TestClass]
    public partial class CSharpUseCrossPlatformIntrinsicsTests
    {
        [TestMethod]
        public void DiagnosticDescriptors_HaveCorrectTitleAndDescription()
        {
            foreach (var rule in Rules)
            {
                Assert.AreEqual(RuleId, rule.Id);
                Assert.IsNotEmpty(rule.Title.ToString());
                Assert.IsNotEmpty(rule.Description.ToString());
                Assert.IsNotEmpty(rule.MessageFormat.ToString());
            }
        }

        [TestMethod]
        public async Task Fixer_InnerNodeReplacedAsync()
        {
            // lang=C#-test
            string testCode = """
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    void M(Vector128<float> x, Vector128<float> y) => Console.WriteLine({|#1:Sse.Add(x, y)|});
                }
                """;

            // lang=C#-test
            string fixedCode = """
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;
                
                class C
                {
                    void M(Vector128<float> x, Vector128<float> y) => Console.WriteLine(x + y);
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Addition]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task Fixer_ChainReplacedAsync()
        {
            // lang=C#-test
            string testCode = """
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    Vector128<float> M(Vector128<float> x, Vector128<float> y, Vector128<float> z) => {|#1:Sse.Add(x, {|#2:Sse.Add(y, z)|})|};
                }
                """;

            // lang=C#-test
            string fixedCode = """
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;
                
                class C
                {
                    Vector128<float> M(Vector128<float> x, Vector128<float> y, Vector128<float> z) => x + (y + z);
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Addition]).WithLocation(1),
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Addition]).WithLocation(2),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task Fixer_ChainParenthesizedAsync()
        {
            // lang=C#-test
            string testCode = """
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    Vector128<float> M(Vector128<float> x, Vector128<float> y, Vector128<float> z) => {|#1:Sse.Multiply(x, {|#2:Sse.Add(y, z)|})|};
                }
                """;

            // lang=C#-test
            string fixedCode = """
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;
                
                class C
                {
                    Vector128<float> M(Vector128<float> x, Vector128<float> y, Vector128<float> z) => x * (y + z);
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Multiply]).WithLocation(1),
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Addition]).WithLocation(2),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task Fixer_FixAllReplacesADeeplyNestedChainInOnePassAsync()
        {
            //  Nesting is the case a shared editor has to get right: replacing an outer node discards the
            //  tracking of everything inside it, so the fixes have to run innermost-first and the outer fix has
            //  to read its operand as already rewritten. Fixer_ChainReplacedAsync covers one level of that,
            //  which an ordering that only happens to work can still pass.
            const int Depth = 8;

            string testExpression = $"{{|#{Depth}:Sse.Add(x, x)|}}";
            string fixedExpression = "x + x";

            //  Numbered outermost-first, so building the chain inside-out counts down.
            for (int depth = Depth - 1; depth >= 1; depth--)
            {
                testExpression = $"{{|#{depth}:Sse.Add(x, {testExpression})|}}";
                fixedExpression = $"x + ({fixedExpression})";
            }

            var test = new VerifyCS.Test
            {
                TestCode = ExpressionBodiedMethod(testExpression),
                FixedCode = ExpressionBodiedMethod(fixedExpression),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            };

            for (int location = 1; location <= Depth; location++)
            {
                test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Addition]).WithLocation(location));
            }

            await test.RunAsync(CancellationToken.None);

            static string ExpressionBodiedMethod(string expression)
                => "using System;\r\n"
                 + "using System.Runtime.Intrinsics;\r\n"
                 + "using System.Runtime.Intrinsics.X86;\r\n"
                 + "\r\n"
                 + "class C\r\n"
                 + "{\r\n"
                 + $"    Vector128<float> M(Vector128<float> x) => {expression};\r\n"
                 + "}";
        }

        [TestMethod]
        public async Task Fixer_FixAllScalesToTwentyIntrinsicsInOneMethodAsync()
        {
            //  Every diagnostic in a document goes through one DocumentEditor, so the count is what exercises
            //  it: an edit that gets dropped or re-reported needs a second fix-all iteration, which the harness
            //  fails on. The tests above place at most two diagnostics, well under the count where that shows.
            const int Count = 20;

            var test = new VerifyCS.Test
            {
                TestCode = MethodBody(Enumerable.Range(0, Count).Select(i => $"_ = {{|#{i + 1}:Sse.Add(x, y)|}};")),
                FixedCode = MethodBody(Enumerable.Repeat("_ = x + y;", Count)),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            };

            for (int location = 1; location <= Count; location++)
            {
                test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Addition]).WithLocation(location));
            }

            await test.RunAsync(CancellationToken.None);

            static string MethodBody(IEnumerable<string> statements)
                => "using System;\r\n"
                 + "using System.Runtime.Intrinsics;\r\n"
                 + "using System.Runtime.Intrinsics.X86;\r\n"
                 + "\r\n"
                 + "class C\r\n"
                 + "{\r\n"
                 + "    void M(Vector128<float> x, Vector128<float> y)\r\n"
                 + "    {\r\n"
                 + string.Concat(statements.Select(statement => $"        {statement}\r\n"))
                 + "    }\r\n"
                 + "}";
        }
    }
}
