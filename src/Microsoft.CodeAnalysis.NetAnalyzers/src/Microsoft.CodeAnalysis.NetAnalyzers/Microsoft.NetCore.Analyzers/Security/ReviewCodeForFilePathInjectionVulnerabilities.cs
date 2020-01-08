// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Analyzer.Utilities.FlowAnalysis.Analysis.TaintedDataAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class ReviewCodeForFilePathInjectionVulnerabilities : SourceTriggeredTaintedDataAnalyzerBase
    {
        internal static readonly DiagnosticDescriptor Rule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA3003",
            nameof(MicrosoftNetCoreAnalyzersResources.ReviewCodeForFilePathInjectionVulnerabilitiesTitle),
            nameof(MicrosoftNetCoreAnalyzersResources.ReviewCodeForFilePathInjectionVulnerabilitiesMessage),
            isEnabledByDefault: false,
            helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca3003-review-code-for-file-path-injection-vulnerabilities");

        protected override SinkKind SinkKind => SinkKind.FilePathInjection;

        protected override DiagnosticDescriptor TaintedDataEnteringSinkDescriptor { get { return Rule; } }
    }
}
