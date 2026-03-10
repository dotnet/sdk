// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.DoNotUseInsecureDtdProcessingAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.DoNotUseInsecureDtdProcessingAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetFramework.Analyzers.UnitTests
{
    public partial class DoNotUseInsecureDtdProcessingAnalyzerTests
    {
        private static async Task VerifyCSharpAnalyzerAsync(
            ReferenceAssemblies referenceAssemblies,
            string source,
            params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = referenceAssemblies,
                TestState =
                {
                    Sources = { source },
                }
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        private static async Task VerifyVisualBasicAnalyzerAsync(
            ReferenceAssemblies referenceAssemblies,
            string source,
            params DiagnosticResult[] expected)
        {
            var visualBasicTest = new VerifyVB.Test
            {
                ReferenceAssemblies = referenceAssemblies,
                TestState =
                {
                    Sources = { source },
                }
            };

            visualBasicTest.ExpectedDiagnostics.AddRange(expected);

            await visualBasicTest.RunAsync();
        }
    }
}
