// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2027: <inheritdoc cref="MicrosoftNetCoreAnalyzersResources.AvoidRedundantRegexIsMatchBeforeMatchMessage"/>
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class AvoidRedundantRegexIsMatchBeforeMatchFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(AvoidRedundantRegexIsMatchBeforeMatch.RuleId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            // TODO: Implement code fix to transform the pattern
            // For now, only provide the diagnostic without an automatic fix
            // The fix would need to:
            // 1. Replace the IsMatch condition with a pattern match on Match result
            // 2. Remove the redundant Match call in the body
            // 3. Adjust variable declarations accordingly
            return Task.CompletedTask;
        }
    }
}
