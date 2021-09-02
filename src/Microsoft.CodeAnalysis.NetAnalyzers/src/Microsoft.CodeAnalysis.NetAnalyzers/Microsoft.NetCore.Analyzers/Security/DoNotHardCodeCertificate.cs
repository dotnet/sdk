// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Analyzer.Utilities.FlowAnalysis.Analysis.TaintedDataAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    using static MicrosoftNetCoreAnalyzersResources;

    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class DoNotHardCodeCertificate : SourceTriggeredTaintedDataAnalyzerBase
    {
        internal static readonly DiagnosticDescriptor Rule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5403",
            nameof(DoNotHardCodeCertificate),
            nameof(DoNotHardCodeCertificateMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: true,
            isReportedAtCompilationEnd: false,
            descriptionResourceStringName: nameof(DoNotHardCodeCertificateDescription));

        protected override SinkKind SinkKind => SinkKind.HardcodedCertificate;

        protected override DiagnosticDescriptor TaintedDataEnteringSinkDescriptor => Rule;
    }
}
