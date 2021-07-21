// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2207: Initialize value type static fields inline
    /// </summary>
    public abstract class InitializeStaticFieldsInlineFixer<TLanguageKindEnum> : CodeFixProvider
        where TLanguageKindEnum : struct
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray<string>.Empty;

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            // TODO: Implement the fixer.
            // Fixer not yet implemented.
            return Task.CompletedTask;
        }
    }
}