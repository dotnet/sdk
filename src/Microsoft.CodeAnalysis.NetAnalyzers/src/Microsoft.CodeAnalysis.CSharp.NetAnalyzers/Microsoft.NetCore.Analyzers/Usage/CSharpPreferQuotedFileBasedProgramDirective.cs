// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CSharpPreferQuotedFileBasedProgramDirective : PreferQuotedFileBasedProgramDirective
{
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxTreeAction(context =>
        {
            var root = context.Tree.GetRoot(context.CancellationToken);
            foreach (var trivia in root.GetLeadingTrivia())
            {
                if (!FileBasedProgramDirectiveQuoting.TryParse(trivia, out var kind, out var value, out _))
                {
                    continue;
                }

                if (!FileBasedProgramDirectiveQuoting.IsLegacyForm(kind, value))
                {
                    continue;
                }

                context.ReportDiagnostic(trivia.GetLocation().CreateDiagnostic(Rule, kind));
            }
        });
    }
}
    