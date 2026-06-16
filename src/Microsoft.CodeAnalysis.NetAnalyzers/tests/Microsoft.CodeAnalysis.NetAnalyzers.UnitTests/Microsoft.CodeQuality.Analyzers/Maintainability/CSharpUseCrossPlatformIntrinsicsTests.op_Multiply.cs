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
        [Theory]
        [InlineData("byte", "AdvSimd.Multiply")]
        [InlineData("sbyte", "AdvSimd.Multiply")]
        [InlineData("short", "AdvSimd.Multiply")]
        [InlineData("ushort", "AdvSimd.Multiply")]
        [InlineData("int", "AdvSimd.Multiply")]
        [InlineData("uint", "AdvSimd.Multiply")]
        [InlineData("float", "AdvSimd.Multiply")]
        [InlineData("double", "AdvSimd.MultiplyScalar")]
        public async Task Fixer_opMultiplyArmV64Async(string type, string method)
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
                    Vector64<{{type}}> M(Vector64<{{type}}> x, Vector64<{{type}}> y) => x * y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Multiply]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync();
        }

        [Theory]
        [InlineData("float", "AdvSimd.MultiplyScalar")]
        public async Task Fixer_opMultiplyArmV64Async_NoReplacement(string type, string method)
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "AdvSimd.Multiply")]
        [InlineData("sbyte", "AdvSimd.Multiply")]
        [InlineData("short", "AdvSimd.Multiply")]
        [InlineData("ushort", "AdvSimd.Multiply")]
        [InlineData("int", "AdvSimd.Multiply")]
        [InlineData("uint", "AdvSimd.Multiply")]
        [InlineData("float", "AdvSimd.Multiply")]
        [InlineData("double", "AdvSimd.Arm64.Multiply")]
        public async Task Fixer_opMultiplyArmV128Async(string type, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x * y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Multiply]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync();
        }

        [Theory]
        [InlineData("short", "PackedSimd.Multiply")]
        [InlineData("ushort", "PackedSimd.Multiply")]
        [InlineData("int", "PackedSimd.Multiply")]
        [InlineData("uint", "PackedSimd.Multiply")]
        [InlineData("long", "PackedSimd.Multiply")]
        [InlineData("ulong", "PackedSimd.Multiply")]
        [InlineData("float", "PackedSimd.Multiply")]
        [InlineData("double", "PackedSimd.Multiply")]
        public async Task Fixer_opMultiplyWasmV128Async(string type, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x * y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Multiply]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync();
        }

        [Theory]
        [InlineData("short", "Sse2.MultiplyLow")]
        [InlineData("ushort", "Sse2.MultiplyLow")]
        [InlineData("int", "Sse41.MultiplyLow")]
        [InlineData("uint", "Sse41.MultiplyLow")]
        [InlineData("long", "Avx512DQ.VL.MultiplyLow")]
        [InlineData("ulong", "Avx512DQ.VL.MultiplyLow")]
        [InlineData("float", "Sse.Multiply")]
        [InlineData("double", "Sse2.Multiply")]
        public async Task Fixer_opMultiplyx86V128Async(string type, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x * y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Multiply]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync();
        }

        [Theory]
        [InlineData("int", "long", "Sse41.Multiply")]
        [InlineData("uint", "ulong", "Sse2.Multiply")]
        public async Task Fixer_opMultiplyx86V128Async_NoReplacement(string type, string retType, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    Vector128<{{retType}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => {{method}}(x, y);
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("short", "Avx2.MultiplyLow")]
        [InlineData("ushort", "Avx2.MultiplyLow")]
        [InlineData("int", "Avx2.MultiplyLow")]
        [InlineData("uint", "Avx2.MultiplyLow")]
        [InlineData("long", "Avx512DQ.VL.MultiplyLow")]
        [InlineData("ulong", "Avx512DQ.VL.MultiplyLow")]
        [InlineData("float", "Avx.Multiply")]
        [InlineData("double", "Avx.Multiply")]
        public async Task Fixer_opMultiplyx86V256Async(string type, string method)
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
                    Vector256<{{type}}> M(Vector256<{{type}}> x, Vector256<{{type}}> y) => x * y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Multiply]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync();
        }

        [Theory]
        [InlineData("int", "long", "Avx2.Multiply")]
        [InlineData("uint", "ulong", "Avx2.Multiply")]
        public async Task Fixer_opMultiplyx86V256Async_NoReplacement(string type, string retType, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    Vector256<{{retType}}> M(Vector256<{{type}}> x, Vector256<{{type}}> y) => {{method}}(x, y);
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("short", "Avx512BW.MultiplyLow")]
        [InlineData("ushort", "Avx512BW.MultiplyLow")]
        [InlineData("int", "Avx512F.MultiplyLow")]
        [InlineData("uint", "Avx512F.MultiplyLow")]
        [InlineData("long", "Avx512DQ.MultiplyLow")]
        [InlineData("ulong", "Avx512DQ.MultiplyLow")]
        [InlineData("float", "Avx512F.Multiply")]
        [InlineData("double", "Avx512F.Multiply")]
        public async Task Fixer_opMultiplyx86V512Async(string type, string method)
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
                    Vector512<{{type}}> M(Vector512<{{type}}> x, Vector512<{{type}}> y) => x * y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Multiply]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync();
        }

        [Theory]
        [InlineData("int", "long", "Avx512F.Multiply")]
        [InlineData("uint", "ulong", "Avx512F.Multiply")]
        public async Task Fixer_opMultiplyx86V512Async_NoReplacement(string type, string retType, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    Vector512<{{retType}}> M(Vector512<{{type}}> x, Vector512<{{type}}> y) => {{method}}(x, y);
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
            }.RunAsync();
        }
    }
}
