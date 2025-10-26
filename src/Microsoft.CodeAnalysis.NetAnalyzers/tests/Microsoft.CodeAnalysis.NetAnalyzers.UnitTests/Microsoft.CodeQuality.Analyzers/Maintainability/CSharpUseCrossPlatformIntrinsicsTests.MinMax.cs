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
        [InlineData("float", "Sse.Max")]
        [InlineData("float", "Sse.Min")]
        public async Task Fixer_MinMaxSseAsync(string type, string method)
        {
            var ruleKind = method.Contains("Max") ? RuleKind.Max : RuleKind.Min;
            var expectedMethod = method.Contains("Max") ? "Max" : "Min";

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
                    Vector128<{{type}}> M(Vector128<{{type}}> x, Vector128<{{type}}> y) => Vector128.{{expectedMethod}}(x, y);
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)ruleKind]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync();
        }

        [Theory]
        [InlineData("byte", "AdvSimd.Max")]
        [InlineData("byte", "AdvSimd.Min")]
        [InlineData("sbyte", "AdvSimd.Max")]
        [InlineData("sbyte", "AdvSimd.Min")]
        [InlineData("short", "AdvSimd.Max")]
        [InlineData("short", "AdvSimd.Min")]
        [InlineData("ushort", "AdvSimd.Max")]
        [InlineData("ushort", "AdvSimd.Min")]
        [InlineData("int", "AdvSimd.Max")]
        [InlineData("int", "AdvSimd.Min")]
        [InlineData("uint", "AdvSimd.Max")]
        [InlineData("uint", "AdvSimd.Min")]
        [InlineData("float", "AdvSimd.Max")]
        [InlineData("float", "AdvSimd.Min")]
        public async Task Fixer_MinMaxAdvSimdAsync(string type, string method)
        {
            var ruleKind = method.Contains("Max") ? RuleKind.Max : RuleKind.Min;
            var expectedMethod = method.Contains("Max") ? "Max" : "Min";

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
                    Vector64<{{type}}> M(Vector64<{{type}}> x, Vector64<{{type}}> y) => Vector64.{{expectedMethod}}(x, y);
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = {
                    VerifyCS.Diagnostic(Rules[(int)ruleKind]).WithLocation(1),
                },
                FixedCode = fixedCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync();
        }
    }
}
