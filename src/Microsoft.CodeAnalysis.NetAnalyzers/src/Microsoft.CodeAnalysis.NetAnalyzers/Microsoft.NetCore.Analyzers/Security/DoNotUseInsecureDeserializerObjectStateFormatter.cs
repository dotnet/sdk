// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// For detecting deserialization with ObjectStateFormatter, which can result in remote code execution.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class DoNotUseInsecureDeserializerObjectStateFormatter : DoNotUseInsecureDeserializerMethodsBase
    {
        internal static readonly DiagnosticDescriptor RealMethodUsedDescriptor =
            SecurityHelpers.CreateDiagnosticDescriptor(
                "CA2315",
                nameof(ObjectStateFormatterMethodUsedTitle),
                nameof(ObjectStateFormatterMethodUsedMessage),
                RuleLevel.Disabled,
                isPortedFxCopRule: false,
                isDataflowRule: false,
                isReportedAtCompilationEnd: false);

        protected override string DeserializerTypeMetadataName => WellKnownTypeNames.SystemWebUIObjectStateFormatter;

        protected override ImmutableHashSet<string> DeserializationMethodNames =>
            ImmutableHashSet.Create(
                StringComparer.Ordinal,
                "Deserialize");

        protected override DiagnosticDescriptor MethodUsedDescriptor => RealMethodUsedDescriptor;
    }
}
