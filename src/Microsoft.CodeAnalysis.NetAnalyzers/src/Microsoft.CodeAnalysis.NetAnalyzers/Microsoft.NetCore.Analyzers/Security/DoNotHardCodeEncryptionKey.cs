// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Analyzer.Utilities.FlowAnalysis.Analysis.TaintedDataAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    using static MicrosoftNetCoreAnalyzersResources;

    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class DoNotHardCodeEncryptionKey : SourceTriggeredTaintedDataAnalyzerBase
    {
        internal static readonly DiagnosticDescriptor Rule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5390",
            nameof(DoNotHardCodeEncryptionKey),
            nameof(DoNotHardCodeEncryptionKeyMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: true,
            isReportedAtCompilationEnd: false,
            descriptionResourceStringName: nameof(DoNotHardCodeEncryptionKeyDescription));

        protected override SinkKind SinkKind => SinkKind.HardcodedEncryptionKey;

        protected override DiagnosticDescriptor TaintedDataEnteringSinkDescriptor => Rule;
    }
}
