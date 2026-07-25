// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

                    var shebangRequiringDirective = root.GetLeadingTrivia().FirstOrDefault(IsShebangRequiringDirective);
                    if (shebangRequiringDirective == default)
                    {
                        return;
                    }

                    var location = shebangRequiringDirective.GetLocation();
                    context.ReportDiagnostic(location.CreateDiagnostic(Rule));
                });
            });
        }

        private static bool IsShebangRequiringDirective(SyntaxTrivia trivia)
        {
            var structure = trivia.GetStructure();
            if (structure is null)
            {
                return false;
            }

            var content = structure.ChildTokens().FirstOrDefault(static token => token.IsKind(SyntaxKind.StringLiteralToken));
            if (!content.IsKind(SyntaxKind.StringLiteralToken))
            {
                return false;
            }

            var trimmedContent = content.Text.AsSpan().TrimStart();
            return isDirective(trimmedContent, "include") ||
                isDirective(trimmedContent, "ref");

            static bool isDirective(ReadOnlySpan<char> content, string directiveName)
            {
                return content.StartsWith(directiveName, StringComparison.Ordinal) &&
                    // `content` is either exactly the directive name or has other text separated from the directive name by whitespace.
                    (content.Length == directiveName.Length || char.IsWhiteSpace(content[directiveName.Length]));
            }
        }
    }
}
