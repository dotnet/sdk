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
        [DataRow("byte", "AdvSimd.Add")]
        [DataRow("sbyte", "AdvSimd.Add")]
        [DataRow("short", "AdvSimd.Add")]
        [DataRow("ushort", "AdvSimd.Add")]
        [DataRow("int", "AdvSimd.Add")]
        [DataRow("uint", "AdvSimd.Add")]
        [DataRow("long", "AdvSimd.AddScalar")]
        [DataRow("ulong", "AdvSimd.AddScalar")]
        [DataRow("float", "AdvSimd.Add")]
        [DataRow("double", "AdvSimd.AddScalar")]
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("float", "AdvSimd.AddScalar")]
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "AdvSimd.Add")]
        [DataRow("sbyte", "AdvSimd.Add")]
        [DataRow("short", "AdvSimd.Add")]
        [DataRow("ushort", "AdvSimd.Add")]
        [DataRow("int", "AdvSimd.Add")]
        [DataRow("uint", "AdvSimd.Add")]
        [DataRow("long", "AdvSimd.Add")]
        [DataRow("ulong", "AdvSimd.Add")]
        [DataRow("float", "AdvSimd.Add")]
        [DataRow("double", "AdvSimd.Arm64.Add")]
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "PackedSimd.Add")]
        [DataRow("sbyte", "PackedSimd.Add")]
        [DataRow("short", "PackedSimd.Add")]
        [DataRow("ushort", "PackedSimd.Add")]
        [DataRow("int", "PackedSimd.Add")]
        [DataRow("uint", "PackedSimd.Add")]
        [DataRow("long", "PackedSimd.Add")]
        [DataRow("ulong", "PackedSimd.Add")]
        [DataRow("float", "PackedSimd.Add")]
        [DataRow("double", "PackedSimd.Add")]
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "Sse2.Add")]
        [DataRow("sbyte", "Sse2.Add")]
        [DataRow("short", "Sse2.Add")]
        [DataRow("ushort", "Sse2.Add")]
        [DataRow("int", "Sse2.Add")]
        [DataRow("uint", "Sse2.Add")]
        [DataRow("long", "Sse2.Add")]
        [DataRow("ulong", "Sse2.Add")]
        [DataRow("float", "Sse.Add")]
        [DataRow("double", "Sse2.Add")]
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "Avx2.Add")]
        [DataRow("sbyte", "Avx2.Add")]
        [DataRow("short", "Avx2.Add")]
        [DataRow("ushort", "Avx2.Add")]
        [DataRow("int", "Avx2.Add")]
        [DataRow("uint", "Avx2.Add")]
        [DataRow("long", "Avx2.Add")]
        [DataRow("ulong", "Avx2.Add")]
        [DataRow("float", "Avx.Add")]
        [DataRow("double", "Avx.Add")]
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte", "Avx512BW.Add")]
        [DataRow("sbyte", "Avx512BW.Add")]
        [DataRow("short", "Avx512BW.Add")]
        [DataRow("ushort", "Avx512BW.Add")]
        [DataRow("int", "Avx512F.Add")]
        [DataRow("uint", "Avx512F.Add")]
        [DataRow("long", "Avx512F.Add")]
        [DataRow("ulong", "Avx512F.Add")]
        [DataRow("float", "Avx512F.Add")]
        [DataRow("double", "Avx512F.Add")]
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
            }.RunAsync(CancellationToken.None);
        }
    }
}
