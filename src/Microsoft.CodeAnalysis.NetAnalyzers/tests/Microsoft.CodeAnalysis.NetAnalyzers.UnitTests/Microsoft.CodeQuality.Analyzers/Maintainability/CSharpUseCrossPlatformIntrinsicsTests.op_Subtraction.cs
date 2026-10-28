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
        [DataRow("byte", "AdvSimd.Subtract")]
        [DataRow("sbyte", "AdvSimd.Subtract")]
        [DataRow("short", "AdvSimd.Subtract")]
        [DataRow("ushort", "AdvSimd.Subtract")]
        [DataRow("int", "AdvSimd.Subtract")]
        [DataRow("uint", "AdvSimd.Subtract")]
        [DataRow("long", "AdvSimd.SubtractScalar")]
        [DataRow("ulong", "AdvSimd.SubtractScalar")]
        [DataRow("float", "AdvSimd.Subtract")]
        [DataRow("double", "AdvSimd.SubtractScalar")]
        public async Task Fixer_opSubtractionArmV64Async(string type, string method)
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
                    Vector64<{{type}}> M(Vector64<{{type}}> x, Vector64<{{type}}> y) => x - y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Subtraction]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("float", "AdvSimd.SubtractScalar")]
        public async Task Fixer_opSubtractionArmV64Async_NoReplacement(string type, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Arm;

                class C
                {
                    Vector64<{{type}}> M(Vector64<{{type}}> x, Vector64<{{type}}> y) => {{method}}(x, y);
                }
                """;

            // lang=C#-test
            string fixedCode = testCode;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = { },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "AdvSimd.Subtract")]
        [DataRow("sbyte", "AdvSimd.Subtract")]
        [DataRow("short", "AdvSimd.Subtract")]
        [DataRow("ushort", "AdvSimd.Subtract")]
        [DataRow("int", "AdvSimd.Subtract")]
        [DataRow("uint", "AdvSimd.Subtract")]
        [DataRow("long", "AdvSimd.Subtract")]
        [DataRow("ulong", "AdvSimd.Subtract")]
        [DataRow("float", "AdvSimd.Subtract")]
        [DataRow("double", "AdvSimd.Arm64.Subtract")]
        public async Task Fixer_opSubtractionArmV128Async(string type, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x - y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Subtraction]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "PackedSimd.Subtract")]
        [DataRow("sbyte", "PackedSimd.Subtract")]
        [DataRow("short", "PackedSimd.Subtract")]
        [DataRow("ushort", "PackedSimd.Subtract")]
        [DataRow("int", "PackedSimd.Subtract")]
        [DataRow("uint", "PackedSimd.Subtract")]
        [DataRow("long", "PackedSimd.Subtract")]
        [DataRow("ulong", "PackedSimd.Subtract")]
        [DataRow("float", "PackedSimd.Subtract")]
        [DataRow("double", "PackedSimd.Subtract")]
        public async Task Fixer_opSubtractionWasmV128Async(string type, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x - y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Subtraction]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "Sse2.Subtract")]
        [DataRow("sbyte", "Sse2.Subtract")]
        [DataRow("short", "Sse2.Subtract")]
        [DataRow("ushort", "Sse2.Subtract")]
        [DataRow("int", "Sse2.Subtract")]
        [DataRow("uint", "Sse2.Subtract")]
        [DataRow("long", "Sse2.Subtract")]
        [DataRow("ulong", "Sse2.Subtract")]
        [DataRow("float", "Sse.Subtract")]
        [DataRow("double", "Sse2.Subtract")]
        public async Task Fixer_opSubtractionx86V128Async(string type, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x - y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Subtraction]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "Avx2.Subtract")]
        [DataRow("sbyte", "Avx2.Subtract")]
        [DataRow("short", "Avx2.Subtract")]
        [DataRow("ushort", "Avx2.Subtract")]
        [DataRow("int", "Avx2.Subtract")]
        [DataRow("uint", "Avx2.Subtract")]
        [DataRow("long", "Avx2.Subtract")]
        [DataRow("ulong", "Avx2.Subtract")]
        [DataRow("float", "Avx.Subtract")]
        [DataRow("double", "Avx.Subtract")]
        public async Task Fixer_opSubtractionx86V256Async(string type, string method)
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
                    Vector256<{{type}}> M(Vector256<{{type}}> x, Vector256<{{type}}> y) => x - y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Subtraction]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "Avx512BW.Subtract")]
        [DataRow("sbyte", "Avx512BW.Subtract")]
        [DataRow("short", "Avx512BW.Subtract")]
        [DataRow("ushort", "Avx512BW.Subtract")]
        [DataRow("int", "Avx512F.Subtract")]
        [DataRow("uint", "Avx512F.Subtract")]
        [DataRow("long", "Avx512F.Subtract")]
        [DataRow("ulong", "Avx512F.Subtract")]
        [DataRow("float", "Avx512F.Subtract")]
        [DataRow("double", "Avx512F.Subtract")]
        public async Task Fixer_opSubtractionx86V512Async(string type, string method)
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
                    Vector512<{{type}}> M(Vector512<{{type}}> x, Vector512<{{type}}> y) => x - y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Subtraction]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }
    }
}
