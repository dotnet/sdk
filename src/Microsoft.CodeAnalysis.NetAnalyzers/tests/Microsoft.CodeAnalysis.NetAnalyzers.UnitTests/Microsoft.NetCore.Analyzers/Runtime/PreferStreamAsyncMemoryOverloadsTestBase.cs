// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferStreamAsyncMemoryOverloads,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreferStreamAsyncMemoryOverloadsTestBase
    {
        #region Helpers

        protected static async Task AnalyzeAsync(string source, params DiagnosticResult[] expected)
        {
            await AnalyzeForVersionAsync(source, ReferenceAssemblies.NetCore.NetCoreApp50, expected);
        }

        protected static async Task AnalyzeUnsupportedAsync(string source, params DiagnosticResult[] expected)
        {
            await AnalyzeForVersionAsync(source, ReferenceAssemblies.NetCore.NetCoreApp20, expected);
        }

        private static async Task AnalyzeForVersionAsync(string source, ReferenceAssemblies version, params DiagnosticResult[] expected)
        {
            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = version,
            };

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync();
        }

        protected static DiagnosticResult GetCSharpResultBase(int line, int column, DiagnosticDescriptor rule)
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column);

        #endregion
    }
}
