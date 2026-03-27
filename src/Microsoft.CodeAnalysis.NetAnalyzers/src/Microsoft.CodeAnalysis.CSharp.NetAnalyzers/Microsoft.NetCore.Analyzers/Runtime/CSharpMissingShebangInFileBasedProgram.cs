// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Linq;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    /// <summary>CA2026: <inheritdoc cref="MissingShebangInFileBasedProgram"/></summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpMissingShebangInFileBasedProgram : MissingShebangInFileBasedProgram
    {
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var globalOptions = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
                if (!globalOptions.TryGetValue("build_property.EntryPointFilePath", out var entryPointFilePath)
                    || string.IsNullOrEmpty(entryPointFilePath))
                {
                    return;
                }

                // Only warn when there are multiple syntax trees (i.e., #:include directives are used).
                // Single-file programs don't need a shebang to distinguish the entry point.
                if (context.Compilation.SyntaxTrees.Count() <= 1)
                {
                    return;
                }

                context.RegisterSyntaxTreeAction(context =>
                {
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
                    context.ReportDiagnostic(location.CreateDiagnostic(Rule));
                });
            });
        }
    }
}
