// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpMissingShebangInFileBasedProgram : MissingShebangInFileBasedProgram
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

                // Count non-generated trees in the compilation upfront.
                // We avoid CompilationEnd so diagnostics appear as live IDE diagnostics.
                int nonGeneratedTreeCount = 0;
                foreach (var tree in context.Compilation.SyntaxTrees)
                {
                    if (context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree)
                            .TryGetValue("generated_code", out var generatedValue) &&
                        generatedValue.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    nonGeneratedTreeCount++;
                }

                // Only report when there are multiple non-generated files
                // (i.e., #:include directives are used).
                // Single-file programs don't need a shebang to distinguish the entry point.
                if (nonGeneratedTreeCount <= 1)
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
                    if (root.GetLeadingTrivia().Any(SyntaxKind.ShebangDirectiveTrivia))
                    {
                        return;
                    }

                    var location = root.GetFirstToken(includeZeroWidth: true).GetLocation();
                    context.ReportDiagnostic(location.CreateDiagnostic(Rule));
                });
            });
        }
    }
}
