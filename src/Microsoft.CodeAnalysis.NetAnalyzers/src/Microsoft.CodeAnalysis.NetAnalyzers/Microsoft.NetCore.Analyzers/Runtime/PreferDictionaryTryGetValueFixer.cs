// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    public abstract class PreferDictionaryTryGetValueFixer : CodeFixProvider
    {
        protected const string Value = "value";
        protected const string TryGetValue = nameof(TryGetValue);
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(PreferDictionaryTryGetValueAnalyzer.RuleId);

        protected static string PreferDictionaryTryGetValueCodeFixTitle => MicrosoftNetCoreAnalyzersResources.PreferDictionaryTryGetValueCodeFixTitle;

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
    }
}