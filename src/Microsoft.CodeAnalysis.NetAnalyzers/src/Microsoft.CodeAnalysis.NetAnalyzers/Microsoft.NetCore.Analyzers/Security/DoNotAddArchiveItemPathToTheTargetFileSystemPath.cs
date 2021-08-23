// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Analyzer.Utilities.FlowAnalysis.Analysis.TaintedDataAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class DoNotAddArchiveItemPathToTheTargetFileSystemPath : SourceTriggeredTaintedDataAnalyzerBase
    {
        internal const string RuleId = "CA5389";

        internal static DiagnosticDescriptor Rule = SecurityHelpers.CreateDiagnosticDescriptor(
            RuleId,
            typeof(MicrosoftNetCoreAnalyzersResources),
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotAddArchiveItemPathToTheTargetFileSystemPath),
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotAddArchiveItemPathToTheTargetFileSystemPathMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: true,
            isReportedAtCompilationEnd: false,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.DoNotAddArchiveItemPathToTheTargetFileSystemPathDescription));

        protected override SinkKind SinkKind => SinkKind.ZipSlip;

        protected override DiagnosticDescriptor TaintedDataEnteringSinkDescriptor => Rule;
    }
}
