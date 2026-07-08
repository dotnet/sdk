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
        [DataRow("byte", "AdvSimd.Not")]
        [DataRow("sbyte", "AdvSimd.Not")]
        [DataRow("short", "AdvSimd.Not")]
        [DataRow("ushort", "AdvSimd.Not")]
        [DataRow("int", "AdvSimd.Not")]
        [DataRow("uint", "AdvSimd.Not")]
        [DataRow("long", "AdvSimd.Not")]
        [DataRow("ulong", "AdvSimd.Not")]
        [DataRow("float", "AdvSimd.Not")]
        [DataRow("double", "AdvSimd.Not")]
        public async Task Fixer_opOnesComplementArmV64Async(string type, string method)
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
                    Vector64<{{type}}> M(Vector64<{{type}}> x) => ~x;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_OnesComplement]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "AdvSimd.Not")]
        [DataRow("sbyte", "AdvSimd.Not")]
        [DataRow("short", "AdvSimd.Not")]
        [DataRow("ushort", "AdvSimd.Not")]
        [DataRow("int", "AdvSimd.Not")]
        [DataRow("uint", "AdvSimd.Not")]
        [DataRow("long", "AdvSimd.Not")]
        [DataRow("ulong", "AdvSimd.Not")]
        [DataRow("float", "AdvSimd.Not")]
        [DataRow("double", "AdvSimd.Not")]
        public async Task Fixer_opOnesComplementArmV128Async(string type, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x) => ~x;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_OnesComplement]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "PackedSimd.Not")]
        [DataRow("sbyte", "PackedSimd.Not")]
        [DataRow("short", "PackedSimd.Not")]
        [DataRow("ushort", "PackedSimd.Not")]
        [DataRow("int", "PackedSimd.Not")]
        [DataRow("uint", "PackedSimd.Not")]
        [DataRow("long", "PackedSimd.Not")]
        [DataRow("ulong", "PackedSimd.Not")]
        [DataRow("float", "PackedSimd.Not")]
        [DataRow("double", "PackedSimd.Not")]
        public async Task Fixer_opOnesComplementWasmV128Async(string type, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x) => ~x;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_OnesComplement]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }
    }
}
