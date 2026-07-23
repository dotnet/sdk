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
        [DataRow("byte", "AdvSimd.And")]
        [DataRow("sbyte", "AdvSimd.And")]
        [DataRow("short", "AdvSimd.And")]
        [DataRow("ushort", "AdvSimd.And")]
        [DataRow("int", "AdvSimd.And")]
        [DataRow("uint", "AdvSimd.And")]
        [DataRow("long", "AdvSimd.And")]
        [DataRow("ulong", "AdvSimd.And")]
        [DataRow("float", "AdvSimd.And")]
        [DataRow("double", "AdvSimd.And")]
        public async Task Fixer_opBitwiseAndArmV64Async(string type, string method)
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
                    Vector64<{{type}}> M(Vector64<{{type}}> x, Vector64<{{type}}> y) => x & y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_BitwiseAnd]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "AdvSimd.And")]
        [DataRow("sbyte", "AdvSimd.And")]
        [DataRow("short", "AdvSimd.And")]
        [DataRow("ushort", "AdvSimd.And")]
        [DataRow("int", "AdvSimd.And")]
        [DataRow("uint", "AdvSimd.And")]
        [DataRow("long", "AdvSimd.And")]
        [DataRow("ulong", "AdvSimd.And")]
        [DataRow("float", "AdvSimd.And")]
        [DataRow("double", "AdvSimd.And")]
        public async Task Fixer_opBitwiseAndArmV128Async(string type, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x & y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_BitwiseAnd]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "PackedSimd.And")]
        [DataRow("sbyte", "PackedSimd.And")]
        [DataRow("short", "PackedSimd.And")]
        [DataRow("ushort", "PackedSimd.And")]
        [DataRow("int", "PackedSimd.And")]
        [DataRow("uint", "PackedSimd.And")]
        [DataRow("long", "PackedSimd.And")]
        [DataRow("ulong", "PackedSimd.And")]
        [DataRow("float", "PackedSimd.And")]
        [DataRow("double", "PackedSimd.And")]
        public async Task Fixer_opBitwiseAndWasmV128Async(string type, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x & y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_BitwiseAnd]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "Sse2.And")]
        [DataRow("sbyte", "Sse2.And")]
        [DataRow("short", "Sse2.And")]
        [DataRow("ushort", "Sse2.And")]
        [DataRow("int", "Sse2.And")]
        [DataRow("uint", "Sse2.And")]
        [DataRow("long", "Sse2.And")]
        [DataRow("ulong", "Sse2.And")]
        [DataRow("float", "Sse.And")]
        [DataRow("double", "Sse2.And")]
        public async Task Fixer_opBitwiseAndx86V128Async(string type, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x & y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_BitwiseAnd]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "Avx2.And")]
        [DataRow("sbyte", "Avx2.And")]
        [DataRow("short", "Avx2.And")]
        [DataRow("ushort", "Avx2.And")]
        [DataRow("int", "Avx2.And")]
        [DataRow("uint", "Avx2.And")]
        [DataRow("long", "Avx2.And")]
        [DataRow("ulong", "Avx2.And")]
        [DataRow("float", "Avx.And")]
        [DataRow("double", "Avx.And")]
        public async Task Fixer_opBitwiseAndx86V256Async(string type, string method)
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
                    Vector256<{{type}}> M(Vector256<{{type}}> x, Vector256<{{type}}> y) => x & y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_BitwiseAnd]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "Avx512F.And")]
        [DataRow("sbyte", "Avx512F.And")]
        [DataRow("short", "Avx512F.And")]
        [DataRow("ushort", "Avx512F.And")]
        [DataRow("int", "Avx512F.And")]
        [DataRow("uint", "Avx512F.And")]
        [DataRow("long", "Avx512F.And")]
        [DataRow("ulong", "Avx512F.And")]
        [DataRow("float", "Avx512DQ.And")]
        [DataRow("double", "Avx512DQ.And")]
        public async Task Fixer_opBitwiseAndx86V512Async(string type, string method)
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
                    Vector512<{{type}}> M(Vector512<{{type}}> x, Vector512<{{type}}> y) => x & y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_BitwiseAnd]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }
    }
}
