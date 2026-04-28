// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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