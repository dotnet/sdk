// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
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

                // Track whether there are multiple non-generated source files.
                // ConfigureGeneratedCodeAnalysis(None) ensures that the SyntaxTreeAction
                // is only called for non-generated trees, so we count those.
                int nonGeneratedTreeCount = 0;
                Diagnostic? pendingDiagnostic = null;

                context.RegisterSyntaxTreeAction(context =>
                {
                    Interlocked.Increment(ref nonGeneratedTreeCount);

                    if (!context.Tree.FilePath.Equals(entryPointFilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    var root = context.Tree.GetRoot(context.CancellationToken);
                    if (root.GetLeadingTrivia().Any(SyntaxKind.ShebangDirectiveTrivia))
                    {
                        return;
                    }

                    var location = root.GetFirstToken(includeZeroWidth: true).GetLocation();
                    Interlocked.CompareExchange(ref pendingDiagnostic, location.CreateDiagnostic(Rule), null);
                });

                context.RegisterCompilationEndAction(context =>
                {
                    // Only report when there are multiple non-generated files
                    // (i.e., #:include directives are used).
                    // Single-file programs don't need a shebang to distinguish the entry point.
                    if (Volatile.Read(ref nonGeneratedTreeCount) > 1 && pendingDiagnostic is { } diagnostic)
                    {
                        context.ReportDiagnostic(diagnostic);
                    }
                });
            });
        }
    }
}
