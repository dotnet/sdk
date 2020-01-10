// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    /// <summary>
    /// For detecting deserialization with ObjectStateFormatter, which can result in remote code execution.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class DoNotUseInsecureDeserializerObjectStateFormatter : DoNotUseInsecureDeserializerMethodsBase
    {
        internal static DiagnosticDescriptor RealMethodUsedDescriptor =
            SecurityHelpers.CreateDiagnosticDescriptor(
                "CA2315",
                nameof(MicrosoftNetCoreAnalyzersResources.ObjectStateFormatterMethodUsedTitle),
                nameof(MicrosoftNetCoreAnalyzersResources.ObjectStateFormatterMethodUsedMessage),
                RuleLevel.Disabled,
                isPortedFxCopRule: false,
                isDataflowRule: false);

        protected override string DeserializerTypeMetadataName => WellKnownTypeNames.SystemWebUIObjectStateFormatter;

        protected override ImmutableHashSet<string> DeserializationMethodNames =>
            ImmutableHashSet.Create(
                StringComparer.Ordinal,
                "Deserialize");

        protected override DiagnosticDescriptor MethodUsedDescriptor => RealMethodUsedDescriptor;
    }
}
