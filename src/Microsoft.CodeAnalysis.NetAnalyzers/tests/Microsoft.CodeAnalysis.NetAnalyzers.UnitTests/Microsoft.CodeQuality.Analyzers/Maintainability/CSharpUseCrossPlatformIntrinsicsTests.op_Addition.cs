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
        [InlineData("byte", "AdvSimd.Add")]
        [InlineData("sbyte", "AdvSimd.Add")]
        [InlineData("short", "AdvSimd.Add")]
        [InlineData("ushort", "AdvSimd.Add")]
        [InlineData("int", "AdvSimd.Add")]
        [InlineData("uint", "AdvSimd.Add")]
        [InlineData("long", "AdvSimd.AddScalar")]
        [InlineData("ulong", "AdvSimd.AddScalar")]
        [InlineData("float", "AdvSimd.Add")]
        [InlineData("double", "AdvSimd.AddScalar")]
        public async Task Fixer_opAdditionArmV64Async(string type, string method)
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
                    Vector64<{{type}}> M(Vector64<{{type}}> x, Vector64<{{type}}> y) => x + y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Addition]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync();
        }

        [Theory]
        [InlineData("float", "AdvSimd.AddScalar")]
        public async Task Fixer_opAdditionArmV64Async_NoReplacement(string type, string method)
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
        [InlineData("byte", "AdvSimd.Add")]
        [InlineData("sbyte", "AdvSimd.Add")]
        [InlineData("short", "AdvSimd.Add")]
        [InlineData("ushort", "AdvSimd.Add")]
        [InlineData("int", "AdvSimd.Add")]
        [InlineData("uint", "AdvSimd.Add")]
        [InlineData("long", "AdvSimd.Add")]
        [InlineData("ulong", "AdvSimd.Add")]
        [InlineData("float", "AdvSimd.Add")]
        [InlineData("double", "AdvSimd.Arm64.Add")]
        public async Task Fixer_opAdditionArmV128Async(string type, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x + y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Addition]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "PackedSimd.Add")]
        [InlineData("sbyte", "PackedSimd.Add")]
        [InlineData("short", "PackedSimd.Add")]
        [InlineData("ushort", "PackedSimd.Add")]
        [InlineData("int", "PackedSimd.Add")]
        [InlineData("uint", "PackedSimd.Add")]
        [InlineData("long", "PackedSimd.Add")]
        [InlineData("ulong", "PackedSimd.Add")]
        [InlineData("float", "PackedSimd.Add")]
        [InlineData("double", "PackedSimd.Add")]
        public async Task Fixer_opAdditionWasmV128Async(string type, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x + y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Addition]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "Sse2.Add")]
        [InlineData("sbyte", "Sse2.Add")]
        [InlineData("short", "Sse2.Add")]
        [InlineData("ushort", "Sse2.Add")]
        [InlineData("int", "Sse2.Add")]
        [InlineData("uint", "Sse2.Add")]
        [InlineData("long", "Sse2.Add")]
        [InlineData("ulong", "Sse2.Add")]
        [InlineData("float", "Sse.Add")]
        [InlineData("double", "Sse2.Add")]
        public async Task Fixer_opAdditionx86V128Async(string type, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => x + y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Addition]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "Avx2.Add")]
        [InlineData("sbyte", "Avx2.Add")]
        [InlineData("short", "Avx2.Add")]
        [InlineData("ushort", "Avx2.Add")]
        [InlineData("int", "Avx2.Add")]
        [InlineData("uint", "Avx2.Add")]
        [InlineData("long", "Avx2.Add")]
        [InlineData("ulong", "Avx2.Add")]
        [InlineData("float", "Avx.Add")]
        [InlineData("double", "Avx.Add")]
        public async Task Fixer_opAdditionx86V256Async(string type, string method)
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
                    Vector256<{{type}}> M(Vector256<{{type}}> x, Vector256<{{type}}> y) => x + y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Addition]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "Avx512BW.Add")]
        [InlineData("sbyte", "Avx512BW.Add")]
        [InlineData("short", "Avx512BW.Add")]
        [InlineData("ushort", "Avx512BW.Add")]
        [InlineData("int", "Avx512F.Add")]
        [InlineData("uint", "Avx512F.Add")]
        [InlineData("long", "Avx512F.Add")]
        [InlineData("ulong", "Avx512F.Add")]
        [InlineData("float", "Avx512F.Add")]
        [InlineData("double", "Avx512F.Add")]
        public async Task Fixer_opAdditionx86V512Async(string type, string method)
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
                    Vector512<{{type}}> M(Vector512<{{type}}> x, Vector512<{{type}}> y) => x + y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_Addition]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync();
        }
    }
}
