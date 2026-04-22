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
        [InlineData("byte", "AdvSimd.Or")]
        [InlineData("sbyte", "AdvSimd.Or")]
        [InlineData("short", "AdvSimd.Or")]
        [InlineData("ushort", "AdvSimd.Or")]
        [InlineData("int", "AdvSimd.Or")]
        [InlineData("uint", "AdvSimd.Or")]
        [InlineData("long", "AdvSimd.Or")]
        [InlineData("ulong", "AdvSimd.Or")]
        [InlineData("float", "AdvSimd.Or")]
        [InlineData("double", "AdvSimd.Or")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "AdvSimd.Or")]
        [InlineData("sbyte", "AdvSimd.Or")]
        [InlineData("short", "AdvSimd.Or")]
        [InlineData("ushort", "AdvSimd.Or")]
        [InlineData("int", "AdvSimd.Or")]
        [InlineData("uint", "AdvSimd.Or")]
        [InlineData("long", "AdvSimd.Or")]
        [InlineData("ulong", "AdvSimd.Or")]
        [InlineData("float", "AdvSimd.Or")]
        [InlineData("double", "AdvSimd.Or")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "PackedSimd.Or")]
        [InlineData("sbyte", "PackedSimd.Or")]
        [InlineData("short", "PackedSimd.Or")]
        [InlineData("ushort", "PackedSimd.Or")]
        [InlineData("int", "PackedSimd.Or")]
        [InlineData("uint", "PackedSimd.Or")]
        [InlineData("long", "PackedSimd.Or")]
        [InlineData("ulong", "PackedSimd.Or")]
        [InlineData("float", "PackedSimd.Or")]
        [InlineData("double", "PackedSimd.Or")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "Sse2.Or")]
        [InlineData("sbyte", "Sse2.Or")]
        [InlineData("short", "Sse2.Or")]
        [InlineData("ushort", "Sse2.Or")]
        [InlineData("int", "Sse2.Or")]
        [InlineData("uint", "Sse2.Or")]
        [InlineData("long", "Sse2.Or")]
        [InlineData("ulong", "Sse2.Or")]
        [InlineData("float", "Sse.Or")]
        [InlineData("double", "Sse2.Or")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "Avx2.Or")]
        [InlineData("sbyte", "Avx2.Or")]
        [InlineData("short", "Avx2.Or")]
        [InlineData("ushort", "Avx2.Or")]
        [InlineData("int", "Avx2.Or")]
        [InlineData("uint", "Avx2.Or")]
        [InlineData("long", "Avx2.Or")]
        [InlineData("ulong", "Avx2.Or")]
        [InlineData("float", "Avx.Or")]
        [InlineData("double", "Avx.Or")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "Avx512F.Or")]
        [InlineData("sbyte", "Avx512F.Or")]
        [InlineData("short", "Avx512F.Or")]
        [InlineData("ushort", "Avx512F.Or")]
        [InlineData("int", "Avx512F.Or")]
        [InlineData("uint", "Avx512F.Or")]
        [InlineData("long", "Avx512F.Or")]
        [InlineData("ulong", "Avx512F.Or")]
        [InlineData("float", "Avx512DQ.Or")]
        [InlineData("double", "Avx512DQ.Or")]
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
            }.RunAsync();
        }
    }
}
