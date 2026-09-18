// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Usage;

public abstract class PreferQuotedFileBasedProgramDirectiveFixer : SyntaxEditorBasedCodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(PreferQuotedFileBasedProgramDirective.RuleId);
}
