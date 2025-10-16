// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.Maintainability.CSharpUseCrossPlatformIntrinsicsAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.Maintainability.CSharpUseCrossPlatformIntrinsicsFixer>;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.UnitTests
{
    using static UseCrossPlatformIntrinsicsAnalyzer;

    public partial class CSharpUseCrossPlatformIntrinsicsTests
    {
        [Fact]
        public void DiagnosticDescriptors_HaveCorrectTitleAndDescription()
        {
            // Verify that all diagnostic descriptors have the expected title and description
            foreach (var rule in Rules)
            {
                Assert.Equal(RuleId, rule.Id);
                Assert.Equal("Use cross-platform intrinsics", rule.Title.ToString());
                Assert.Equal("This rule detects usage of platform-specific intrinsics that can be replaced with an equivalent cross-platform intrinsic instead.", rule.Description.ToString());
                Assert.NotEmpty(rule.MessageFormat.ToString());
            }
        }

        [Fact]
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
            }.RunAsync();
        }

        [Fact]
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
            }.RunAsync();
        }

        [Fact]
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
            }.RunAsync();
        }
    }
}
