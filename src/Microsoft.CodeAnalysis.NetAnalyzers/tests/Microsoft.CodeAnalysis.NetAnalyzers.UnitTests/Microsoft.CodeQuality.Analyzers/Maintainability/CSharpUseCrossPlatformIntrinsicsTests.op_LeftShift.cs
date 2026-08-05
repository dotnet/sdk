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
        [DataRow("byte", "7", "AdvSimd.ShiftLeftLogical")]
        [DataRow("sbyte", "7", "AdvSimd.ShiftLeftLogical")]
        [DataRow("short", "15", "AdvSimd.ShiftLeftLogical")]
        [DataRow("ushort", "15", "AdvSimd.ShiftLeftLogical")]
        [DataRow("int", "31", "AdvSimd.ShiftLeftLogical")]
        [DataRow("uint", "31", "AdvSimd.ShiftLeftLogical")]
        [DataRow("long", "63", "AdvSimd.ShiftLeftLogicalScalar")]
        [DataRow("ulong", "63", "AdvSimd.ShiftLeftLogicalScalar")]
        public async Task Fixer_opLeftShiftArmV64Async(string type, string max, string method)
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
                    Vector64<{{type}}> M(Vector64<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x << y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_LeftShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "7", "AdvSimd.ShiftLeftLogical")]
        [DataRow("sbyte", "7", "AdvSimd.ShiftLeftLogical")]
        [DataRow("short", "15", "AdvSimd.ShiftLeftLogical")]
        [DataRow("ushort", "15", "AdvSimd.ShiftLeftLogical")]
        // The int32 overload does not exist today:
        //   [InlineData("int", "31", "AdvSimd.ShiftLeftLogical")]
        [DataRow("uint", "31", "AdvSimd.ShiftLeftLogical")]
        [DataRow("long", "63", "AdvSimd.ShiftLeftLogical")]
        [DataRow("ulong", "63", "AdvSimd.ShiftLeftLogical")]
        public async Task Fixer_opLeftShiftArmV128Async(string type, string max, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x << y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_LeftShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "7", "PackedSimd.ShiftLeft")]
        [DataRow("sbyte", "7", "PackedSimd.ShiftLeft")]
        [DataRow("short", "15", "PackedSimd.ShiftLeft")]
        [DataRow("ushort", "15", "PackedSimd.ShiftLeft")]
        [DataRow("int", "31", "PackedSimd.ShiftLeft")]
        [DataRow("uint", "31", "PackedSimd.ShiftLeft")]
        [DataRow("long", "63", "PackedSimd.ShiftLeft")]
        [DataRow("ulong", "63", "PackedSimd.ShiftLeft")]
        public async Task Fixer_opLeftShiftWasmV128Async(string type, string max, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x << y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_LeftShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("short", "15", "Sse2.ShiftLeftLogical")]
        [DataRow("ushort", "15", "Sse2.ShiftLeftLogical")]
        [DataRow("int", "31", "Sse2.ShiftLeftLogical")]
        [DataRow("uint", "31", "Sse2.ShiftLeftLogical")]
        [DataRow("long", "63", "Sse2.ShiftLeftLogical")]
        [DataRow("ulong", "63", "Sse2.ShiftLeftLogical")]
        public async Task Fixer_opLeftShiftx86V128Async(string type, string max, string method)
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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x << y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_LeftShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("short", "15", "Avx2.ShiftLeftLogical")]
        [DataRow("ushort", "15", "Avx2.ShiftLeftLogical")]
        [DataRow("int", "31", "Avx2.ShiftLeftLogical")]
        [DataRow("uint", "31", "Avx2.ShiftLeftLogical")]
        [DataRow("long", "63", "Avx2.ShiftLeftLogical")]
        [DataRow("ulong", "63", "Avx2.ShiftLeftLogical")]
        public async Task Fixer_opLeftShiftx86V256Async(string type, string max, string method)
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
                    Vector256<{{type}}> M(Vector256<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x << y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_LeftShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("short", "15", "Avx512BW.ShiftLeftLogical")]
        [DataRow("ushort", "15", "Avx512BW.ShiftLeftLogical")]
        [DataRow("int", "31", "Avx512F.ShiftLeftLogical")]
        [DataRow("uint", "31", "Avx512F.ShiftLeftLogical")]
        [DataRow("long", "63", "Avx512F.ShiftLeftLogical")]
        [DataRow("ulong", "63", "Avx512F.ShiftLeftLogical")]
        public async Task Fixer_opLeftShiftx86V512Async(string type, string max, string method)
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
                    Vector512<{{type}}> M(Vector512<{{type}}> x, [ConstantExpected(Max = (byte)({{max}}))] byte y) => x << y;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_LeftShift]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }
    }
}
