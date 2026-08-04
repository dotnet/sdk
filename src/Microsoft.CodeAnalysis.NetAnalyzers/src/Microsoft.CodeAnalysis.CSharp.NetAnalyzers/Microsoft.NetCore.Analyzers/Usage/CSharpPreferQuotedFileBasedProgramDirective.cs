// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpPreferQuotedFileBasedProgramDirective : PreferQuotedFileBasedProgramDirective
    {
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var entryPointFilePath = context.Options.GetMSBuildPropertyValue(
                    MSBuildPropertyOptionNames.EntryPointFilePath, context.Compilation);
                if (string.IsNullOrEmpty(entryPointFilePath))
                {
                    return;
                }

                context.RegisterSyntaxTreeAction(context =>
                {
                    if (!context.Tree.FilePath.Equals(entryPointFilePath, StringComparison.Ordinal))
                    {
                        return;
                    }

                    var root = context.Tree.GetRoot(context.CancellationToken);
                    foreach (var trivia in root.GetLeadingTrivia())
                    {
                        if (!FileBasedProgramDirectiveQuoting.TryParse(trivia, out var kind, out var value))
                        {
                            continue;
                        }

                        if (!FileBasedProgramDirectiveQuoting.TryGetQuotedForm(kind, value, out _))
                        {
                            continue;
                        }

                        context.ReportDiagnostic(trivia.GetLocation().CreateDiagnostic(Rule, kind));
                    }
                });
            });
        }
    }
}
