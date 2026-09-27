// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    [TestProperty(Traits.DataflowAnalysis, Traits.Dataflow.TaintedDataAnalysis)]
    public abstract class TaintedDataAnalyzerTestBase<TCSharpAnalyzer, TVisualBasicAnalyzer>
        where TCSharpAnalyzer : DiagnosticAnalyzer, new()
        where TVisualBasicAnalyzer : DiagnosticAnalyzer, new()
    {
        protected abstract DiagnosticDescriptor Rule { get; }

        protected virtual IEnumerable<string> AdditionalCSharpSources { get; }

        protected virtual IEnumerable<string> AdditionalVisualBasicSources { get; }

        protected static string RepeatedByteArraySourcesWithoutReachableSink()
        {
            string calls = string.Concat(Enumerable.Repeat("            Bytes(1, 2, 3, 4, 5, 6, 7, 8);\n", 64));
            return $$"""
                class Encoder
                {
                    private void Bytes(params byte[] values) { }

                    public void Emit()
                    {
                {{calls}}
                        var aes = System.Security.Cryptography.Aes.Create();
                        aes.CreateEncryptor();
                        var certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2();
                    }

                    public void Unrelated(byte[] bytes)
                    {
                        using (var aes = System.Security.Cryptography.Aes.Create())
                        {
                            aes.Key = bytes;
                        }

                        var certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(bytes);
                    }
                }
                """;
        }

        protected static string RepeatedWebInputWithoutReachableSink()
        {
            string calls = string.Concat(Enumerable.Repeat("        Bytes(1, 2, 3, 4, 5, 6, 7, 8);\n", 64));
            return $$"""
                using System.Data;

                class WebForm : System.Web.UI.Page
                {
                    private void Bytes(params byte[] values) { }

                    protected int Emit()
                    {
                        string input = Request.Form["in"];
                {{calls}}
                        return input.Length;
                    }

                    protected void Unrelated(IDbCommand command, string text)
                    {
                        command.CommandText = text;
                        Response.Write(text);
                    }
                }
                """;
        }

        protected const string WebInputWithSinkReachedThroughMethod = """
            using System.Data;

            class WebForm : System.Web.UI.Page
            {
                protected void Emit(IDbCommand command)
                {
                    string input = Request.Form["in"];
                    UseInput(command, input);
                }

                private void UseInput(IDbCommand command, string input)
                {
                    command.CommandText = input;
                    Response.Write(input);
                }
            }
            """;

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
            await test.RunAsync(CancellationToken.None);
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
            await test.RunAsync(CancellationToken.None);
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
            await test.RunAsync(CancellationToken.None);
        }
    }
}
