// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Microsoft.NetFramework.Analyzers
{
    /// <summary>
    /// CA1306: Set locale for data types
    /// </summary>
    public abstract class SetLocaleForDataTypesFixer : CodeFixProvider
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