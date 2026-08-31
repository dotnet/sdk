// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Usage;

public abstract class PreferQuotedFileBasedProgramDirective : DiagnosticAnalyzer
{
    internal const string RuleId = "CA2267";

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        RuleId,
        CreateLocalizableResourceString(nameof(PreferQuotedFileBasedProgramDirectiveTitle)),
        CreateLocalizableResourceString(nameof(PreferQuotedFileBasedProgramDirectiveMessage)),
        DiagnosticCategory.Usage,
        RuleLevel.BuildWarning,
        CreateLocalizableResourceString(nameof(PreferQuotedFileBasedProgramDirectiveDescription)),
        isPortedFxCopRule: false,
        isDataflowRule: false,
        isReportedAtCompilationEnd: false);

    public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];
}
