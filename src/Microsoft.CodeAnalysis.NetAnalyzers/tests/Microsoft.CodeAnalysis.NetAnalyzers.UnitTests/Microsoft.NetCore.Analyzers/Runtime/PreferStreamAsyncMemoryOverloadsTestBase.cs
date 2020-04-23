// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferStreamAsyncMemoryOverloads,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferStreamAsyncMemoryOverloads,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreferStreamAsyncMemoryOverloadsTestBase
    {
        protected static Task AnalyzeCSAsync(string source, params DiagnosticResult[] expected) =>
            AnalyzeCSForVersionAsync(source, ReferenceAssemblies.NetCore.NetCoreApp50, expected);

        protected static Task AnalyzeCSUnsupportedAsync(string source, params DiagnosticResult[] expected) =>
            AnalyzeCSForVersionAsync(source, ReferenceAssemblies.NetCore.NetCoreApp20, expected);

        protected static Task AnalyzeVBAsync(string source, params DiagnosticResult[] expected) =>
            AnalyzeVBForVersionAsync(source, ReferenceAssemblies.NetCore.NetCoreApp50, expected);

        protected static Task AnalyzeVBUnsupportedAsync(string source, params DiagnosticResult[] expected) =>
            AnalyzeVBForVersionAsync(source, ReferenceAssemblies.NetCore.NetCoreApp20, expected);

        protected static DiagnosticResult GetCSResultForRule(int startLine, int startColumn, int endLine, int endColumn, DiagnosticDescriptor rule)
            => VerifyCS.Diagnostic(rule)
                .WithSpan(startLine, startColumn, endLine, endColumn);

        protected static DiagnosticResult GetVBResultForRule(int startLine, int startColumn, int endLine, int endColumn, DiagnosticDescriptor rule)
            => VerifyVB.Diagnostic(rule)
                .WithSpan(startLine, startColumn, endLine, endColumn);

        private static Task AnalyzeCSForVersionAsync(string source, ReferenceAssemblies version, params DiagnosticResult[] expected)
        {
            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = version,
            };

            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync();
        }

        private static Task AnalyzeVBForVersionAsync(string source, ReferenceAssemblies version, params DiagnosticResult[] expected)
        {
            var test = new VerifyVB.Test
            {
                TestCode = source,
                ReferenceAssemblies = version,
            };

            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync();
        }
    }
}