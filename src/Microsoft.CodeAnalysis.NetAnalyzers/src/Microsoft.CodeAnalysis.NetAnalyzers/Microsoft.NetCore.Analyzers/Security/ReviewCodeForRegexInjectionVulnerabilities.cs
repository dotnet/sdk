// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Analyzer.Utilities.FlowAnalysis.Analysis.TaintedDataAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class ReviewCodeForRegexInjectionVulnerabilities : SourceTriggeredTaintedDataAnalyzerBase
    {
        internal static readonly DiagnosticDescriptor Rule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA3012",
            nameof(MicrosoftNetCoreAnalyzersResources.ReviewCodeForRegexInjectionVulnerabilitiesTitle),
            nameof(MicrosoftNetCoreAnalyzersResources.ReviewCodeForRegexInjectionVulnerabilitiesMessage),
            isEnabledByDefault: false,
            helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca3012-review-code-for-regex-injection-vulnerabilities");

        protected override SinkKind SinkKind { get { return SinkKind.Regex; } }

        protected override DiagnosticDescriptor TaintedDataEnteringSinkDescriptor { get { return Rule; } }
    }
}
