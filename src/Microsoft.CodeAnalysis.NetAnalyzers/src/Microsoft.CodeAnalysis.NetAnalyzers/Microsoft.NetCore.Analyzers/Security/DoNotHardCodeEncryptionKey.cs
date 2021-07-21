// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Analyzer.Utilities.FlowAnalysis.Analysis.TaintedDataAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class DoNotHardCodeEncryptionKey : SourceTriggeredTaintedDataAnalyzerBase
    {
        internal static DiagnosticDescriptor Rule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5390",
            typeof(MicrosoftNetCoreAnalyzersResources),
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotHardCodeEncryptionKey),
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotHardCodeEncryptionKeyMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: true,
            isReportedAtCompilationEnd: false,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.DoNotHardCodeEncryptionKeyDescription));

        protected override SinkKind SinkKind => SinkKind.HardcodedEncryptionKey;

        protected override DiagnosticDescriptor TaintedDataEnteringSinkDescriptor => Rule;
    }
}
