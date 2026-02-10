// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.NetCore.Analyzers.Usage
{
    public abstract class DoNotCompareSpanToNullFixer : CodeFixProvider
    {
        protected const string IsEmpty = nameof(IsEmpty);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DoNotCompareSpanToNullAnalyzer.RuleId);
    }
}