// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferStreamAsyncMemoryOverloads,
    Microsoft.NetCore.Analyzers.Runtime.PreferStreamAsyncMemoryOverloadsFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreferStreamAsyncMemoryOverloadsTestBase
    {
        #region Helpers

        protected static async Task VerifyAnalyzerAsync50(string source, params DiagnosticResult[] expected)
        {
            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.NetCore.NetCoreApp31,
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
