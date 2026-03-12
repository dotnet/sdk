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
        [InlineData("byte", "AdvSimd.Subtract")]
        [InlineData("sbyte", "AdvSimd.Subtract")]
        [InlineData("short", "AdvSimd.Subtract")]
        [InlineData("ushort", "AdvSimd.Subtract")]
        [InlineData("int", "AdvSimd.Subtract")]
        [InlineData("uint", "AdvSimd.Subtract")]
        [InlineData("long", "AdvSimd.SubtractScalar")]
        [InlineData("ulong", "AdvSimd.SubtractScalar")]
        [InlineData("float", "AdvSimd.Subtract")]
        [InlineData("double", "AdvSimd.SubtractScalar")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("float", "AdvSimd.SubtractScalar")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "AdvSimd.Subtract")]
        [InlineData("sbyte", "AdvSimd.Subtract")]
        [InlineData("short", "AdvSimd.Subtract")]
        [InlineData("ushort", "AdvSimd.Subtract")]
        [InlineData("int", "AdvSimd.Subtract")]
        [InlineData("uint", "AdvSimd.Subtract")]
        [InlineData("long", "AdvSimd.Subtract")]
        [InlineData("ulong", "AdvSimd.Subtract")]
        [InlineData("float", "AdvSimd.Subtract")]
        [InlineData("double", "AdvSimd.Arm64.Subtract")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "PackedSimd.Subtract")]
        [InlineData("sbyte", "PackedSimd.Subtract")]
        [InlineData("short", "PackedSimd.Subtract")]
        [InlineData("ushort", "PackedSimd.Subtract")]
        [InlineData("int", "PackedSimd.Subtract")]
        [InlineData("uint", "PackedSimd.Subtract")]
        [InlineData("long", "PackedSimd.Subtract")]
        [InlineData("ulong", "PackedSimd.Subtract")]
        [InlineData("float", "PackedSimd.Subtract")]
        [InlineData("double", "PackedSimd.Subtract")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "Sse2.Subtract")]
        [InlineData("sbyte", "Sse2.Subtract")]
        [InlineData("short", "Sse2.Subtract")]
        [InlineData("ushort", "Sse2.Subtract")]
        [InlineData("int", "Sse2.Subtract")]
        [InlineData("uint", "Sse2.Subtract")]
        [InlineData("long", "Sse2.Subtract")]
        [InlineData("ulong", "Sse2.Subtract")]
        [InlineData("float", "Sse.Subtract")]
        [InlineData("double", "Sse2.Subtract")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "Avx2.Subtract")]
        [InlineData("sbyte", "Avx2.Subtract")]
        [InlineData("short", "Avx2.Subtract")]
        [InlineData("ushort", "Avx2.Subtract")]
        [InlineData("int", "Avx2.Subtract")]
        [InlineData("uint", "Avx2.Subtract")]
        [InlineData("long", "Avx2.Subtract")]
        [InlineData("ulong", "Avx2.Subtract")]
        [InlineData("float", "Avx.Subtract")]
        [InlineData("double", "Avx.Subtract")]
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
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "Avx512BW.Subtract")]
        [InlineData("sbyte", "Avx512BW.Subtract")]
        [InlineData("short", "Avx512BW.Subtract")]
        [InlineData("ushort", "Avx512BW.Subtract")]
        [InlineData("int", "Avx512F.Subtract")]
        [InlineData("uint", "Avx512F.Subtract")]
        [InlineData("long", "Avx512F.Subtract")]
        [InlineData("ulong", "Avx512F.Subtract")]
        [InlineData("float", "Avx512F.Subtract")]
        [InlineData("double", "Avx512F.Subtract")]
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
            }.RunAsync();
        }
    }
}
