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
        [DataRow("sbyte", "7", "AdvSimd.ShiftRightArithmetic")]
        [DataRow("short", "15", "AdvSimd.ShiftRightArithmetic")]
        [DataRow("int", "31", "AdvSimd.ShiftRightArithmetic")]
        [DataRow("long", "63", "AdvSimd.ShiftRightArithmeticScalar")]
        public async Task Fixer_opRightShiftArmV64Async(string type, string max, string method)
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
                    Vector64<{{type}}> M(Vector64<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x >> y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_RightShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("sbyte", "7", "AdvSimd.ShiftRightArithmetic")]
        [DataRow("short", "15", "AdvSimd.ShiftRightArithmetic")]
        [DataRow("int", "31", "AdvSimd.ShiftRightArithmetic")]
        [DataRow("long", "63", "AdvSimd.ShiftRightArithmetic")]
        public async Task Fixer_opRightShiftArmV128Async(string type, string max, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x >> y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_RightShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("sbyte", "7", "PackedSimd.ShiftRightArithmetic")]
        [DataRow("short", "15", "PackedSimd.ShiftRightArithmetic")]
        [DataRow("int", "31", "PackedSimd.ShiftRightArithmetic")]
        [DataRow("long", "63", "PackedSimd.ShiftRightArithmetic")]
        public async Task Fixer_opRightShiftWasmV128Async(string type, string max, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x >> y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_RightShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("short", "15", "Sse2.ShiftRightArithmetic")]
        [DataRow("int", "31", "Sse2.ShiftRightArithmetic")]
        [DataRow("long", "63", "Avx512F.VL.ShiftRightArithmetic")]
        public async Task Fixer_opRightShiftx86V128Async(string type, string max, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x >> y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_RightShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("short", "15", "Avx2.ShiftRightArithmetic")]
        [DataRow("int", "31", "Avx2.ShiftRightArithmetic")]
        [DataRow("long", "63", "Avx512F.VL.ShiftRightArithmetic")]
        public async Task Fixer_opRightShiftx86V256Async(string type, string max, string method)
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
                    Vector256<{{type}}> M(Vector256<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x >> y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_RightShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("short", "15", "Avx512BW.ShiftRightArithmetic")]
        [DataRow("int", "31", "Avx512F.ShiftRightArithmetic")]
        [DataRow("long", "63", "Avx512F.ShiftRightArithmetic")]
        public async Task Fixer_opRightShiftx86V512Async(string type, string max, string method)
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
                    Vector512<{{type}}> M(Vector512<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x >> y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_RightShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }
    }
}
