// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    /// <summary>
    /// CA2205: Use managed equivalents of win32 api
    /// </summary>
    public abstract class UseManagedEquivalentsOfWin32ApiFixer : CodeFixProvider
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