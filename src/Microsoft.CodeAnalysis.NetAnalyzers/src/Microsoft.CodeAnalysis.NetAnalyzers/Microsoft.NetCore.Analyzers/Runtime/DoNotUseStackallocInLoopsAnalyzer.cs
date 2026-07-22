// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2014: <inheritdoc cref="DoNotUseStackallocInLoopsTitle"/>
    /// </summary>
    public abstract class DoNotUseStackallocInLoopsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2014";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotUseStackallocInLoopsTitle)),
            CreateLocalizableResourceString(nameof(DoNotUseStackallocInLoopsMessage)),
            DiagnosticCategory.Reliability,
            RuleLevel.BuildWarning,
            CreateLocalizableResourceString(nameof(DoNotUseStackallocInLoopsDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);
    }
}
