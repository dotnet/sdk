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
        [InlineData("byte", "AdvSimd.Xor")]
        [InlineData("sbyte", "AdvSimd.Xor")]
        [InlineData("short", "AdvSimd.Xor")]
        [InlineData("ushort", "AdvSimd.Xor")]
        [InlineData("int", "AdvSimd.Xor")]
        [InlineData("uint", "AdvSimd.Xor")]
        [InlineData("long", "AdvSimd.Xor")]
        [InlineData("ulong", "AdvSimd.Xor")]
        [InlineData("float", "AdvSimd.Xor")]
        [InlineData("double", "AdvSimd.Xor")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "AdvSimd.Xor")]
        [InlineData("sbyte", "AdvSimd.Xor")]
        [InlineData("short", "AdvSimd.Xor")]
        [InlineData("ushort", "AdvSimd.Xor")]
        [InlineData("int", "AdvSimd.Xor")]
        [InlineData("uint", "AdvSimd.Xor")]
        [InlineData("long", "AdvSimd.Xor")]
        [InlineData("ulong", "AdvSimd.Xor")]
        [InlineData("float", "AdvSimd.Xor")]
        [InlineData("double", "AdvSimd.Xor")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "PackedSimd.Xor")]
        [InlineData("sbyte", "PackedSimd.Xor")]
        [InlineData("short", "PackedSimd.Xor")]
        [InlineData("ushort", "PackedSimd.Xor")]
        [InlineData("int", "PackedSimd.Xor")]
        [InlineData("uint", "PackedSimd.Xor")]
        [InlineData("long", "PackedSimd.Xor")]
        [InlineData("ulong", "PackedSimd.Xor")]
        [InlineData("float", "PackedSimd.Xor")]
        [InlineData("double", "PackedSimd.Xor")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "Sse2.Xor")]
        [InlineData("sbyte", "Sse2.Xor")]
        [InlineData("short", "Sse2.Xor")]
        [InlineData("ushort", "Sse2.Xor")]
        [InlineData("int", "Sse2.Xor")]
        [InlineData("uint", "Sse2.Xor")]
        [InlineData("long", "Sse2.Xor")]
        [InlineData("ulong", "Sse2.Xor")]
        [InlineData("float", "Sse.Xor")]
        [InlineData("double", "Sse2.Xor")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "Avx2.Xor")]
        [InlineData("sbyte", "Avx2.Xor")]
        [InlineData("short", "Avx2.Xor")]
        [InlineData("ushort", "Avx2.Xor")]
        [InlineData("int", "Avx2.Xor")]
        [InlineData("uint", "Avx2.Xor")]
        [InlineData("long", "Avx2.Xor")]
        [InlineData("ulong", "Avx2.Xor")]
        [InlineData("float", "Avx.Xor")]
        [InlineData("double", "Avx.Xor")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "Avx512F.Xor")]
        [InlineData("sbyte", "Avx512F.Xor")]
        [InlineData("short", "Avx512F.Xor")]
        [InlineData("ushort", "Avx512F.Xor")]
        [InlineData("int", "Avx512F.Xor")]
        [InlineData("uint", "Avx512F.Xor")]
        [InlineData("long", "Avx512F.Xor")]
        [InlineData("ulong", "Avx512F.Xor")]
        [InlineData("float", "Avx512DQ.Xor")]
        [InlineData("double", "Avx512DQ.Xor")]
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
            }.RunAsync();
        }
    }
}
