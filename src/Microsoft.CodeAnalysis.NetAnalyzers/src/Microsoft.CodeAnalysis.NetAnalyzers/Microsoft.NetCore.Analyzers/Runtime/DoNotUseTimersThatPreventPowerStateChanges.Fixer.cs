// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1601: Do not use timers that prevent power state changes
    /// </summary>
    public abstract class DoNotUseTimersThatPreventPowerStateChangesFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray<string>.Empty;

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