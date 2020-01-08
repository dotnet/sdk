// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Analyzer.Utilities.FlowAnalysis.Analysis.TaintedDataAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class ReviewCodeForXPathInjectionVulnerabilities : SourceTriggeredTaintedDataAnalyzerBase
    {
        internal static readonly DiagnosticDescriptor Rule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA3008",
            nameof(MicrosoftNetCoreAnalyzersResources.ReviewCodeForXPathInjectionVulnerabilitiesTitle),
            nameof(MicrosoftNetCoreAnalyzersResources.ReviewCodeForXPathInjectionVulnerabilitiesMessage),
            isEnabledByDefault: false,
            helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca3008-review-code-for-xpath-injection-vulnerabilities");

        protected override SinkKind SinkKind { get { return SinkKind.XPath; } }

        protected override DiagnosticDescriptor TaintedDataEnteringSinkDescriptor { get { return Rule; } }
    }
}
