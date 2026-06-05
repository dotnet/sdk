// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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