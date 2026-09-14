// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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