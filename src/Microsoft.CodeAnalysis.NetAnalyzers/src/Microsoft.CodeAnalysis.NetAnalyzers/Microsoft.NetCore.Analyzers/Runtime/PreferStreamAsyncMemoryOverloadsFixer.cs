// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1834: Prefer Memory/ReadOnlyMemory overloads for Stream ReadAsync/WriteAsync methods.
    /// </summary>
    public class PreferStreamAsyncMemoryOverloadsFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(PreferStreamAsyncMemoryOverloads.RuleId);
        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context) => Task.CompletedTask;
    }
}
