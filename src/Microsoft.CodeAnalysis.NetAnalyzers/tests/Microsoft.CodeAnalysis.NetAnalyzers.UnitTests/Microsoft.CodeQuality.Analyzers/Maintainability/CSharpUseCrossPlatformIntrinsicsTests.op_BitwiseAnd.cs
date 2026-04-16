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
        [InlineData("byte", "AdvSimd.And")]
        [InlineData("sbyte", "AdvSimd.And")]
        [InlineData("short", "AdvSimd.And")]
        [InlineData("ushort", "AdvSimd.And")]
        [InlineData("int", "AdvSimd.And")]
        [InlineData("uint", "AdvSimd.And")]
        [InlineData("long", "AdvSimd.And")]
        [InlineData("ulong", "AdvSimd.And")]
        [InlineData("float", "AdvSimd.And")]
        [InlineData("double", "AdvSimd.And")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "AdvSimd.And")]
        [InlineData("sbyte", "AdvSimd.And")]
        [InlineData("short", "AdvSimd.And")]
        [InlineData("ushort", "AdvSimd.And")]
        [InlineData("int", "AdvSimd.And")]
        [InlineData("uint", "AdvSimd.And")]
        [InlineData("long", "AdvSimd.And")]
        [InlineData("ulong", "AdvSimd.And")]
        [InlineData("float", "AdvSimd.And")]
        [InlineData("double", "AdvSimd.And")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "PackedSimd.And")]
        [InlineData("sbyte", "PackedSimd.And")]
        [InlineData("short", "PackedSimd.And")]
        [InlineData("ushort", "PackedSimd.And")]
        [InlineData("int", "PackedSimd.And")]
        [InlineData("uint", "PackedSimd.And")]
        [InlineData("long", "PackedSimd.And")]
        [InlineData("ulong", "PackedSimd.And")]
        [InlineData("float", "PackedSimd.And")]
        [InlineData("double", "PackedSimd.And")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "Sse2.And")]
        [InlineData("sbyte", "Sse2.And")]
        [InlineData("short", "Sse2.And")]
        [InlineData("ushort", "Sse2.And")]
        [InlineData("int", "Sse2.And")]
        [InlineData("uint", "Sse2.And")]
        [InlineData("long", "Sse2.And")]
        [InlineData("ulong", "Sse2.And")]
        [InlineData("float", "Sse.And")]
        [InlineData("double", "Sse2.And")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "Avx2.And")]
        [InlineData("sbyte", "Avx2.And")]
        [InlineData("short", "Avx2.And")]
        [InlineData("ushort", "Avx2.And")]
        [InlineData("int", "Avx2.And")]
        [InlineData("uint", "Avx2.And")]
        [InlineData("long", "Avx2.And")]
        [InlineData("ulong", "Avx2.And")]
        [InlineData("float", "Avx.And")]
        [InlineData("double", "Avx.And")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "Avx512F.And")]
        [InlineData("sbyte", "Avx512F.And")]
        [InlineData("short", "Avx512F.And")]
        [InlineData("ushort", "Avx512F.And")]
        [InlineData("int", "Avx512F.And")]
        [InlineData("uint", "Avx512F.And")]
        [InlineData("long", "Avx512F.And")]
        [InlineData("ulong", "Avx512F.And")]
        [InlineData("float", "Avx512DQ.And")]
        [InlineData("double", "Avx512DQ.And")]
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
            }.RunAsync();
        }
    }
}
