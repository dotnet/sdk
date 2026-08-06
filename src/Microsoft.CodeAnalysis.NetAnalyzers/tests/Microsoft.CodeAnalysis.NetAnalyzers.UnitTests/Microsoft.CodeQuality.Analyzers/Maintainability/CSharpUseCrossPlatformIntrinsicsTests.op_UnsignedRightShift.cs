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
        [InlineData("byte", "7", "AdvSimd.ShiftRightLogical")]
        [InlineData("sbyte", "7", "AdvSimd.ShiftRightLogical")]
        [InlineData("short", "15", "AdvSimd.ShiftRightLogical")]
        [InlineData("ushort", "15", "AdvSimd.ShiftRightLogical")]
        [InlineData("int", "31", "AdvSimd.ShiftRightLogical")]
        [InlineData("uint", "31", "AdvSimd.ShiftRightLogical")]
        [InlineData("long", "63", "AdvSimd.ShiftRightLogicalScalar")]
        [InlineData("ulong", "63", "AdvSimd.ShiftRightLogicalScalar")]
        public async Task Fixer_opUnsignedRightShiftArmV64Async(string type, string max, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Arm;

                class C
                {
                    Vector64<{{type}}> M(Vector64<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => {|#1:{{method}}(x, y)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Arm;

                class C
                {
                    Vector64<{{type}}> M(Vector64<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x >>> y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_UnsignedRightShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp11
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "7", "AdvSimd.ShiftRightLogical")]
        [InlineData("sbyte", "7", "AdvSimd.ShiftRightLogical")]
        [InlineData("short", "15", "AdvSimd.ShiftRightLogical")]
        [InlineData("ushort", "15", "AdvSimd.ShiftRightLogical")]
        [InlineData("int", "31", "AdvSimd.ShiftRightLogical")]
        [InlineData("uint", "31", "AdvSimd.ShiftRightLogical")]
        [InlineData("long", "63", "AdvSimd.ShiftRightLogical")]
        [InlineData("ulong", "63", "AdvSimd.ShiftRightLogical")]
        public async Task Fixer_opUnsignedRightShiftArmV128Async(string type, string max, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Arm;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => {|#1:{{method}}(x, y)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Arm;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x >>> y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_UnsignedRightShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp11
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "7", "PackedSimd.ShiftRightLogical")]
        [InlineData("sbyte", "7", "PackedSimd.ShiftRightLogical")]
        [InlineData("short", "15", "PackedSimd.ShiftRightLogical")]
        [InlineData("ushort", "15", "PackedSimd.ShiftRightLogical")]
        [InlineData("int", "31", "PackedSimd.ShiftRightLogical")]
        [InlineData("uint", "31", "PackedSimd.ShiftRightLogical")]
        [InlineData("long", "63", "PackedSimd.ShiftRightLogical")]
        [InlineData("ulong", "63", "PackedSimd.ShiftRightLogical")]
        public async Task Fixer_opUnsignedRightShiftWasmV128Async(string type, string max, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Wasm;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => {|#1:{{method}}(x, y)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Wasm;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x >>> y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_UnsignedRightShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp11
            }.RunAsync();
        }

        [Theory]
        [InlineData("short", "15", "Sse2.ShiftRightLogical")]
        [InlineData("ushort", "15", "Sse2.ShiftRightLogical")]
        [InlineData("int", "31", "Sse2.ShiftRightLogical")]
        [InlineData("uint", "31", "Sse2.ShiftRightLogical")]
        [InlineData("long", "63", "Sse2.ShiftRightLogical")]
        [InlineData("ulong", "63", "Sse2.ShiftRightLogical")]
        public async Task Fixer_opUnsignedRightShiftx86V128Async(string type, string max, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => {|#1:{{method}}(x, y)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x >>> y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_UnsignedRightShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp11
            }.RunAsync();
        }

        [Theory]
        [InlineData("short", "15", "Avx2.ShiftRightLogical")]
        [InlineData("ushort", "15", "Avx2.ShiftRightLogical")]
        [InlineData("int", "31", "Avx2.ShiftRightLogical")]
        [InlineData("uint", "31", "Avx2.ShiftRightLogical")]
        [InlineData("long", "63", "Avx2.ShiftRightLogical")]
        [InlineData("ulong", "63", "Avx2.ShiftRightLogical")]
        public async Task Fixer_opUnsignedRightShiftx86V256Async(string type, string max, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    Vector256<{{type}}> M(Vector256<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => {|#1:{{method}}(x, y)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    Vector256<{{type}}> M(Vector256<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x >>> y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_UnsignedRightShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp11
            }.RunAsync();
        }

        [Theory]
        [InlineData("short", "15", "Avx512BW.ShiftRightLogical")]
        [InlineData("ushort", "15", "Avx512BW.ShiftRightLogical")]
        [InlineData("int", "31", "Avx512F.ShiftRightLogical")]
        [InlineData("uint", "31", "Avx512F.ShiftRightLogical")]
        [InlineData("long", "63", "Avx512F.ShiftRightLogical")]
        [InlineData("ulong", "63", "Avx512F.ShiftRightLogical")]
        public async Task Fixer_opUnsignedRightShiftx86V512Async(string type, string max, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    Vector512<{{type}}> M(Vector512<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => {|#1:{{method}}(x, y)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Diagnostics.CodeAnalysis;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.X86;

                class C
                {
                    Vector512<{{type}}> M(Vector512<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x >>> y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_UnsignedRightShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp11
            }.RunAsync();
        }
    }
}
