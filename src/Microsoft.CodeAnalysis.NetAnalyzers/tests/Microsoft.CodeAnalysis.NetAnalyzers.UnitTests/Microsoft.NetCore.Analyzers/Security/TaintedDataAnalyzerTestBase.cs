// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.TaintedDataAnalysis)]
    public abstract class TaintedDataAnalyzerTestBase<TCSharpAnalyzer, TVisualBasicAnalyzer>
        where TCSharpAnalyzer : DiagnosticAnalyzer, new()
        where TVisualBasicAnalyzer : DiagnosticAnalyzer, new()
    {
        protected abstract DiagnosticDescriptor Rule { get; }

        protected virtual IEnumerable<string> AdditionalCSharpSources { get; }

        protected virtual IEnumerable<string> AdditionalVisualBasicSources { get; }

        protected DiagnosticResult GetCSharpResultAt(int sinkLine, int sinkColumn, int sourceLine, int sourceColumn, string sink, string sinkContainingMethod, string source, string sourceContainingMethod)
        {
#pragma warning disable RS0030 // Do not use banned APIs
#pragma warning disable RS0030 // Do not use banned APIs
            return new DiagnosticResult(Rule).WithArguments(sink, sinkContainingMethod, source, sourceContainingMethod)
                .WithLocation(sinkLine, sinkColumn)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithLocation(sourceLine, sourceColumn);
#pragma warning restore RS0030 // Do not use banned APIs
        }

        protected async Task VerifyCSharpWithDependenciesAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new CSharpSecurityCodeFixVerifier<TCSharpAnalyzer, EmptyCodeFixProvider>.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultForTaintedDataAnalysis
            };
            test.TestState.AdditionalReferences.Add(AdditionalMetadataReferences.TestReferenceAssembly);

            test.TestState.Sources.Add(source);
            if (AdditionalCSharpSources is object)
            {
                foreach (var additionalSource in AdditionalCSharpSources)
                {
                    test.TestState.Sources.Add(additionalSource);
                }
            }

            test.TestState.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync();
        }

        protected async Task VerifyCSharpWithDependenciesAsync(string source, (string additionalFile, string fileContent) file, params DiagnosticResult[] expected)
        {
            var test = new CSharpSecurityCodeFixVerifier<TCSharpAnalyzer, EmptyCodeFixProvider>.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultForTaintedDataAnalysis
            };
            test.TestState.AdditionalReferences.Add(AdditionalMetadataReferences.TestReferenceAssembly);

            test.TestState.Sources.Add(source);
            if (AdditionalCSharpSources is object)
            {
                foreach (var additionalSource in AdditionalCSharpSources)
                {
                    test.TestState.Sources.Add(additionalSource);
                }
            }

            test.TestState.AnalyzerConfigFiles.Add(file);

            test.TestState.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync();
        }

        protected DiagnosticResult GetBasicResultAt(int sinkLine, int sinkColumn, int sourceLine, int sourceColumn, string sink, string sinkContainingMethod, string source, string sourceContainingMethod)
        {
#pragma warning disable RS0030 // Do not use banned APIs
#pragma warning disable RS0030 // Do not use banned APIs
            return new DiagnosticResult(Rule).WithArguments(sink, sinkContainingMethod, source, sourceContainingMethod)
                .WithLocation(sinkLine, sinkColumn)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithLocation(sourceLine, sourceColumn);
#pragma warning restore RS0030 // Do not use banned APIs
        }

        protected async Task VerifyVisualBasicWithDependenciesAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new VisualBasicSecurityCodeFixVerifier<TVisualBasicAnalyzer, EmptyCodeFixProvider>.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultForTaintedDataAnalysis
            };
            test.TestState.AdditionalReferences.Add(AdditionalMetadataReferences.TestReferenceAssembly);

            test.TestState.Sources.Add(source);
            if (AdditionalVisualBasicSources is object)
            {
                foreach (var additionalSource in AdditionalVisualBasicSources)
                {
                    test.TestState.Sources.Add(additionalSource);
                }
            }

            test.TestState.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync();
        }
    }
}
