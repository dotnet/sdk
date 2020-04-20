// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1003: Use generic event handler instances
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class UseGenericEventHandlerInstancesFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray<string>.Empty;

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            // Fixer not yet implemented.
            return Task.CompletedTask;
        }
    }
}