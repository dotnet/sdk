// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    public abstract class PreferDictionaryContainsMethodsFixer : CodeFixProvider
    {
        protected static string KeysPropertyName => PreferDictionaryContainsMethods.KeysPropertyName;

        protected static string ValuesPropertyName => PreferDictionaryContainsMethods.ValuesPropertyName;

        protected static string ContainsKeyMethodName => PreferDictionaryContainsMethods.ContainsKeyMethodName;

        protected static string ContainsValueMethodName => PreferDictionaryContainsMethods.ContainsValueMethodName;

        protected static string ContainsKeyCodeFixTitle => MicrosoftNetCoreAnalyzersResources.PreferDictionaryContainsKeyCodeFixTitle;

        protected static string ContainsValueCodeFixTitle => MicrosoftNetCoreAnalyzersResources.PreferDictionaryContainsValueCodeFixTitle;

        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(PreferDictionaryContainsMethods.RuleId);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
    }
}
