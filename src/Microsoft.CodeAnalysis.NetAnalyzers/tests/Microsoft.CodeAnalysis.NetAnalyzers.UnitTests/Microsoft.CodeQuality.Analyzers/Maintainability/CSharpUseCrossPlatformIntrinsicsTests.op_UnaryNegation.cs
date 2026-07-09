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

    public partial class CSharpUseCrossPlatformIntrinsicsTests
    {
        [TestMethod]
        [DataRow("sbyte", "AdvSimd.Negate")]
        [DataRow("short", "AdvSimd.Negate")]
        [DataRow("int", "AdvSimd.Negate")]
        [DataRow("long", "AdvSimd.Arm64.NegateScalar")]
        [DataRow("float", "AdvSimd.Negate")]
        [DataRow("double", "AdvSimd.NegateScalar")]
        public async Task Fixer_opUnaryNegationArmV64Async(string type, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Arm;

                class C
                {
                    Vector64<{{type}}> M(Vector64<{{type}}> x) => {|#1:{{method}}(x)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Arm;

                class C
                {
                    Vector64<{{type}}> M(Vector64<{{type}}> x) => -x;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_UnaryNegation]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("sbyte", "AdvSimd.Negate")]
        [DataRow("short", "AdvSimd.Negate")]
        [DataRow("int", "AdvSimd.Negate")]
        [DataRow("long", "AdvSimd.Arm64.Negate")]
        [DataRow("float", "AdvSimd.Negate")]
        [DataRow("double", "AdvSimd.Arm64.Negate")]
        public async Task Fixer_opUnaryNegationArmV128Async(string type, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Arm;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x) => {|#1:{{method}}(x)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Arm;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x) => -x;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_UnaryNegation]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("sbyte", "PackedSimd.Negate")]
        [DataRow("short", "PackedSimd.Negate")]
        [DataRow("int", "PackedSimd.Negate")]
        [DataRow("long", "PackedSimd.Negate")]
        [DataRow("float", "PackedSimd.Negate")]
        [DataRow("double", "PackedSimd.Negate")]
        public async Task Fixer_opUnaryNegationWasmV128Async(string type, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Wasm;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x) => {|#1:{{method}}(x)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Wasm;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x) => -x;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_UnaryNegation]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }
    }
}
