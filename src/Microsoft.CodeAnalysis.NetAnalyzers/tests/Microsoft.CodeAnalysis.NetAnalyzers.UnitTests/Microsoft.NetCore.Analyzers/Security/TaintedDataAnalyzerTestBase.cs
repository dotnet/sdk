// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.TaintedDataAnalysis)]
    public abstract class TaintedDataAnalyzerTestBase : DiagnosticAnalyzerTestBase
    {
        public TaintedDataAnalyzerTestBase(ITestOutputHelper output)
            : base(output)
        {
        }

        protected abstract DiagnosticDescriptor Rule { get; }

        protected virtual IEnumerable<string> AdditionalCSharpSources { get; }

        protected DiagnosticResult GetCSharpResultAt(int sinkLine, int sinkColumn, int sourceLine, int sourceColumn, string sink, string sinkContainingMethod, string source, string sourceContainingMethod)
        {
            return GetCSharpResultAt(
                new[] {
                    (sinkLine, sinkColumn),
                    (sourceLine, sourceColumn)
                },
                this.Rule,
                sink,
                sinkContainingMethod,
                source,
                sourceContainingMethod);
        }

        protected void VerifyCSharpWithDependencies(string source, params DiagnosticResult[] expected)
        {
            string[] sources = new string[] { source };
            if (this.AdditionalCSharpSources != null)
            {
                sources = sources.Concat(this.AdditionalCSharpSources).ToArray();
            }

            this.VerifyCSharp(sources, ReferenceFlags.AddTestReferenceAssembly, expected);
        }

        protected void VerifyCSharpWithDependencies(string source, FileAndSource additionalFile, params DiagnosticResult[] expected)
        {
            string[] sources = new string[] { source };
            if (this.AdditionalCSharpSources != null)
            {
                sources = sources.Concat(this.AdditionalCSharpSources).ToArray();
            }

            this.VerifyCSharp(sources, additionalFile, ReferenceFlags.AddTestReferenceAssembly, expected);
        }

        protected DiagnosticResult GetBasicResultAt(int sinkLine, int sinkColumn, int sourceLine, int sourceColumn, string sink, string sinkContainingMethod, string source, string sourceContainingMethod)
        {
            return GetBasicResultAt(
                new[] {
                    (sinkLine, sinkColumn),
                    (sourceLine, sourceColumn)
                },
                this.Rule,
                sink,
                sinkContainingMethod,
                source,
                sourceContainingMethod);
        }

        protected void VerifyBasicWithDependencies(string source, params DiagnosticResult[] expected)
        {
            this.VerifyBasic(source, ReferenceFlags.AddTestReferenceAssembly, expected);
        }
    }
}
