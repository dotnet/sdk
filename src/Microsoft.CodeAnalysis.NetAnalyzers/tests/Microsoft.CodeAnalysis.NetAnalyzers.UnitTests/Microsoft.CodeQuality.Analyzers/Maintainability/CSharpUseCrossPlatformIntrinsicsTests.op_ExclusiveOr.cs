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
        [DataRow("byte", "AdvSimd.Xor")]
        [DataRow("sbyte", "AdvSimd.Xor")]
        [DataRow("short", "AdvSimd.Xor")]
        [DataRow("ushort", "AdvSimd.Xor")]
        [DataRow("int", "AdvSimd.Xor")]
        [DataRow("uint", "AdvSimd.Xor")]
        [DataRow("long", "AdvSimd.Xor")]
        [DataRow("ulong", "AdvSimd.Xor")]
        [DataRow("float", "AdvSimd.Xor")]
        [DataRow("double", "AdvSimd.Xor")]
        public async Task Fixer_opExclusiveOrArmV64Async(string type, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Arm;

                class C
                {
                    Vector64<{{type}}> M(Vector64<{{type}}> x, Vector64<{{type}}> y) => {|#1:{{method}}(x, y)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Arm;

                class C
                {
                    Vector64<{{type}}> M(Vector64<{{type}}> x, Vector64<{{type}}> y) => x ^ y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_ExclusiveOr]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "AdvSimd.Xor")]
        [DataRow("sbyte", "AdvSimd.Xor")]
        [DataRow("short", "AdvSimd.Xor")]
        [DataRow("ushort", "AdvSimd.Xor")]
        [DataRow("int", "AdvSimd.Xor")]
        [DataRow("uint", "AdvSimd.Xor")]
        [DataRow("long", "AdvSimd.Xor")]
        [DataRow("ulong", "AdvSimd.Xor")]
        [DataRow("float", "AdvSimd.Xor")]
        [DataRow("double", "AdvSimd.Xor")]
        public async Task Fixer_opExclusiveOrArmV128Async(string type, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Arm;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => {|#1:{{method}}(x, y)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Arm;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x ^ y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_ExclusiveOr]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "PackedSimd.Xor")]
        [DataRow("sbyte", "PackedSimd.Xor")]
        [DataRow("short", "PackedSimd.Xor")]
        [DataRow("ushort", "PackedSimd.Xor")]
        [DataRow("int", "PackedSimd.Xor")]
        [DataRow("uint", "PackedSimd.Xor")]
        [DataRow("long", "PackedSimd.Xor")]
        [DataRow("ulong", "PackedSimd.Xor")]
        [DataRow("float", "PackedSimd.Xor")]
        [DataRow("double", "PackedSimd.Xor")]
        public async Task Fixer_opExclusiveOrWasmV128Async(string type, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Wasm;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => {|#1:{{method}}(x, y)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Wasm;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x ^ y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_ExclusiveOr]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "Sse2.Xor")]
        [DataRow("sbyte", "Sse2.Xor")]
        [DataRow("short", "Sse2.Xor")]
        [DataRow("ushort", "Sse2.Xor")]
        [DataRow("int", "Sse2.Xor")]
        [DataRow("uint", "Sse2.Xor")]
        [DataRow("long", "Sse2.Xor")]
        [DataRow("ulong", "Sse2.Xor")]
        [DataRow("float", "Sse.Xor")]
        [DataRow("double", "Sse2.Xor")]
        public async Task Fixer_opExclusiveOrx86V128Async(string type, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => {|#1:{{method}}(x, y)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x ^ y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_ExclusiveOr]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "Avx2.Xor")]
        [DataRow("sbyte", "Avx2.Xor")]
        [DataRow("short", "Avx2.Xor")]
        [DataRow("ushort", "Avx2.Xor")]
        [DataRow("int", "Avx2.Xor")]
        [DataRow("uint", "Avx2.Xor")]
        [DataRow("long", "Avx2.Xor")]
        [DataRow("ulong", "Avx2.Xor")]
        [DataRow("float", "Avx.Xor")]
        [DataRow("double", "Avx.Xor")]
        public async Task Fixer_opExclusiveOrx86V256Async(string type, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    Vector256<{{type}}> M(Vector256<{{type}}> x, Vector256<{{type}}> y) => {|#1:{{method}}(x, y)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    Vector256<{{type}}> M(Vector256<{{type}}> x, Vector256<{{type}}> y) => x ^ y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_ExclusiveOr]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "Avx512F.Xor")]
        [DataRow("sbyte", "Avx512F.Xor")]
        [DataRow("short", "Avx512F.Xor")]
        [DataRow("ushort", "Avx512F.Xor")]
        [DataRow("int", "Avx512F.Xor")]
        [DataRow("uint", "Avx512F.Xor")]
        [DataRow("long", "Avx512F.Xor")]
        [DataRow("ulong", "Avx512F.Xor")]
        [DataRow("float", "Avx512DQ.Xor")]
        [DataRow("double", "Avx512DQ.Xor")]
        public async Task Fixer_opExclusiveOrx86V512Async(string type, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    Vector512<{{type}}> M(Vector512<{{type}}> x, Vector512<{{type}}> y) => {|#1:{{method}}(x, y)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    Vector512<{{type}}> M(Vector512<{{type}}> x, Vector512<{{type}}> y) => x ^ y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_ExclusiveOr]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }
    }
}
