// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA2224: Override Equals on overloading operator equals
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpOverrideEqualsOnOverloadingOperatorEqualsFixer : OverrideEqualsOnOverloadingOperatorEqualsFixer
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("CS0660");
    }
}