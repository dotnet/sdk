// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.NetCore.Analyzers.Performance
{
    public abstract class PreferDictionaryTryMethodsOverContainsKeyGuardFixer : CodeFixProvider
    {
        protected const string Value = "value";
        protected const string TryGetValue = nameof(TryGetValue);
        protected const string TryAdd = nameof(TryAdd);

        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
            PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryGetValueRuleId,
            PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryAddRuleId
        );

        protected static string PreferDictionaryTryGetValueCodeFixTitle => MicrosoftNetCoreAnalyzersResources.PreferDictionaryTryGetValueCodeFixTitle;

        protected static string PreferDictionaryTryAddValueCodeFixTitle => MicrosoftNetCoreAnalyzersResources.PreferDictionaryTryAddValueCodeFixTitle;

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
    }
}