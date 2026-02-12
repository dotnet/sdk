// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1726: Use preferred terms
    /// </summary>
    public abstract class UsePreferredTermsFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(UsePreferredTermsAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            // Fixer not yet implemented.
            return Task.CompletedTask;

        }
    }
}