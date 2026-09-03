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
        [InlineData("byte", "AdvSimd.Not")]
        [InlineData("sbyte", "AdvSimd.Not")]
        [InlineData("short", "AdvSimd.Not")]
        [InlineData("ushort", "AdvSimd.Not")]
        [InlineData("int", "AdvSimd.Not")]
        [InlineData("uint", "AdvSimd.Not")]
        [InlineData("long", "AdvSimd.Not")]
        [InlineData("ulong", "AdvSimd.Not")]
        [InlineData("float", "AdvSimd.Not")]
        [InlineData("double", "AdvSimd.Not")]
        public async Task Fixer_opOnesComplementArmV64Async(string type, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Arm;

                class C
                {
                    Vector64<{{type}}> M(Vector64<{{type}}> x) => {|#1:{{method}}(x)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Arm;

                class C
                {
                    Vector64<{{type}}> M(Vector64<{{type}}> x) => ~x;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_OnesComplement]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "AdvSimd.Not")]
        [InlineData("sbyte", "AdvSimd.Not")]
        [InlineData("short", "AdvSimd.Not")]
        [InlineData("ushort", "AdvSimd.Not")]
        [InlineData("int", "AdvSimd.Not")]
        [InlineData("uint", "AdvSimd.Not")]
        [InlineData("long", "AdvSimd.Not")]
        [InlineData("ulong", "AdvSimd.Not")]
        [InlineData("float", "AdvSimd.Not")]
        [InlineData("double", "AdvSimd.Not")]
        public async Task Fixer_opOnesComplementArmV128Async(string type, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Arm;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x) => {|#1:{{method}}(x)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Arm;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x) => ~x;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_OnesComplement]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "PackedSimd.Not")]
        [InlineData("sbyte", "PackedSimd.Not")]
        [InlineData("short", "PackedSimd.Not")]
        [InlineData("ushort", "PackedSimd.Not")]
        [InlineData("int", "PackedSimd.Not")]
        [InlineData("uint", "PackedSimd.Not")]
        [InlineData("long", "PackedSimd.Not")]
        [InlineData("ulong", "PackedSimd.Not")]
        [InlineData("float", "PackedSimd.Not")]
        [InlineData("double", "PackedSimd.Not")]
        public async Task Fixer_opOnesComplementWasmV128Async(string type, string method)
        {
            // lang=C#-test
            string testCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Wasm;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x) => {|#1:{{method}}(x)|};
                }
                """;

            // lang=C#-test
            string fixedCode = $$"""
                using System;
                using System.Runtime.Intrinsics;
                using System.Runtime.Intrinsics.Wasm;

                class C
                {
                    Vector128<{{type}}> M(Vector128<{{type}}> x) => ~x;
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)RuleKind.op_OnesComplement]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync();
        }
    }
}
