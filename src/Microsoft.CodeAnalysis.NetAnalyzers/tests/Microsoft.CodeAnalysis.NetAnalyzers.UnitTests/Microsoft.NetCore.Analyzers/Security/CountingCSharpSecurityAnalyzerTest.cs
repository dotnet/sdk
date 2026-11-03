// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    internal sealed class CountingCSharpSecurityAnalyzerTest<TAnalyzer> : CSharpSecurityCodeFixVerifier<TAnalyzer, EmptyCodeFixProvider>.Test
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        private readonly Func<TAnalyzer> _createAnalyzer;

        public CountingCSharpSecurityAnalyzerTest(Func<TAnalyzer> createAnalyzer)
        {
            _createAnalyzer = createAnalyzer;
        }

        protected override IEnumerable<DiagnosticAnalyzer> GetDiagnosticAnalyzers()
        {
            return new[] { _createAnalyzer() };
        }
    }
}
