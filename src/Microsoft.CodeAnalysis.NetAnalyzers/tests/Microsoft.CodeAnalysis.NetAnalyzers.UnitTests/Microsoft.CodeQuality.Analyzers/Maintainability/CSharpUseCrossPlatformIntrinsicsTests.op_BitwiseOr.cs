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
        [DataRow("byte", "AdvSimd.Or")]
        [DataRow("sbyte", "AdvSimd.Or")]
        [DataRow("short", "AdvSimd.Or")]
        [DataRow("ushort", "AdvSimd.Or")]
        [DataRow("int", "AdvSimd.Or")]
        [DataRow("uint", "AdvSimd.Or")]
        [DataRow("long", "AdvSimd.Or")]
        [DataRow("ulong", "AdvSimd.Or")]
        [DataRow("float", "AdvSimd.Or")]
        [DataRow("double", "AdvSimd.Or")]
        public async Task Fixer_opBitwiseOrArmV64Async(string type, string method)
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
                    Vector64<{{type}}> M(Vector64<{{type}}> x, Vector64<{{type}}> y) => x | y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_BitwiseOr]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "AdvSimd.Or")]
        [DataRow("sbyte", "AdvSimd.Or")]
        [DataRow("short", "AdvSimd.Or")]
        [DataRow("ushort", "AdvSimd.Or")]
        [DataRow("int", "AdvSimd.Or")]
        [DataRow("uint", "AdvSimd.Or")]
        [DataRow("long", "AdvSimd.Or")]
        [DataRow("ulong", "AdvSimd.Or")]
        [DataRow("float", "AdvSimd.Or")]
        [DataRow("double", "AdvSimd.Or")]
        public async Task Fixer_opBitwiseOrArmV128Async(string type, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x | y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_BitwiseOr]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "PackedSimd.Or")]
        [DataRow("sbyte", "PackedSimd.Or")]
        [DataRow("short", "PackedSimd.Or")]
        [DataRow("ushort", "PackedSimd.Or")]
        [DataRow("int", "PackedSimd.Or")]
        [DataRow("uint", "PackedSimd.Or")]
        [DataRow("long", "PackedSimd.Or")]
        [DataRow("ulong", "PackedSimd.Or")]
        [DataRow("float", "PackedSimd.Or")]
        [DataRow("double", "PackedSimd.Or")]
        public async Task Fixer_opBitwiseOrWasmV128Async(string type, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x | y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_BitwiseOr]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "Sse2.Or")]
        [DataRow("sbyte", "Sse2.Or")]
        [DataRow("short", "Sse2.Or")]
        [DataRow("ushort", "Sse2.Or")]
        [DataRow("int", "Sse2.Or")]
        [DataRow("uint", "Sse2.Or")]
        [DataRow("long", "Sse2.Or")]
        [DataRow("ulong", "Sse2.Or")]
        [DataRow("float", "Sse.Or")]
        [DataRow("double", "Sse2.Or")]
        public async Task Fixer_opBitwiseOrx86V128Async(string type, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x | y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_BitwiseOr]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "Avx2.Or")]
        [DataRow("sbyte", "Avx2.Or")]
        [DataRow("short", "Avx2.Or")]
        [DataRow("ushort", "Avx2.Or")]
        [DataRow("int", "Avx2.Or")]
        [DataRow("uint", "Avx2.Or")]
        [DataRow("long", "Avx2.Or")]
        [DataRow("ulong", "Avx2.Or")]
        [DataRow("float", "Avx.Or")]
        [DataRow("double", "Avx.Or")]
        public async Task Fixer_opBitwiseOrx86V256Async(string type, string method)
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
                    Vector256<{{type}}> M(Vector256<{{type}}> x, Vector256<{{type}}> y) => x | y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_BitwiseOr]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "Avx512F.Or")]
        [DataRow("sbyte", "Avx512F.Or")]
        [DataRow("short", "Avx512F.Or")]
        [DataRow("ushort", "Avx512F.Or")]
        [DataRow("int", "Avx512F.Or")]
        [DataRow("uint", "Avx512F.Or")]
        [DataRow("long", "Avx512F.Or")]
        [DataRow("ulong", "Avx512F.Or")]
        [DataRow("float", "Avx512DQ.Or")]
        [DataRow("double", "Avx512DQ.Or")]
        public async Task Fixer_opBitwiseOrx86V512Async(string type, string method)
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
                    Vector512<{{type}}> M(Vector512<{{type}}> x, Vector512<{{type}}> y) => x | y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_BitwiseOr]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }
    }
}
