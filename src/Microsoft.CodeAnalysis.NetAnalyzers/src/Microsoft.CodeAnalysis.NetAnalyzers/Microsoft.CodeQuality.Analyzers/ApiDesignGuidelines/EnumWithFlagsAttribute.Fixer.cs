// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1027: Mark enums with FlagsAttribute
    /// CA2217: Do not mark enums with FlagsAttribute
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class EnumWithFlagsAttributeFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(EnumWithFlagsAttributeAnalyzer.RuleIdMarkEnumsWithFlags,
                                                                                   EnumWithFlagsAttributeAnalyzer.RuleIdDoNotMarkEnumsWithFlags);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SemanticModel model = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            INamedTypeSymbol? flagsAttributeType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemFlagsAttribute);
            if (flagsAttributeType == null)
            {
                return;
            }

            foreach (var diagnostic in context.Diagnostics)
            {
                string fixTitle = GetTitle(diagnostic);
                context.RegisterCodeFix(CodeAction.Create(fixTitle,
                                             ct => SyntaxEditorFixAllProvider.ApplyFixesAsync(context.Document, ImmutableArray.Create(diagnostic), ApplyFixAsync, ct),
                                             equivalenceKey: fixTitle),
                                        diagnostic);
            }
        }

        // The two rules produce opposite fixes, and DocumentBasedFixAllProvider does not filter by
        // CodeActionEquivalenceKey, so a fix-all invoked from one title must skip the other's diagnostics.
        public override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(
                fixAllContext => fixAllContext.CodeActionEquivalenceKey,
                (document, diagnostic, editor, equivalenceKey, cancellationToken) => equivalenceKey is null || GetTitle(diagnostic) == equivalenceKey
                    ? ApplyFixAsync(document, diagnostic, editor, cancellationToken)
                    : Task.CompletedTask);

        private static string GetTitle(Diagnostic diagnostic)
            => diagnostic.Id == EnumWithFlagsAttributeAnalyzer.RuleIdMarkEnumsWithFlags
                ? MicrosoftCodeQualityAnalyzersResources.MarkEnumsWithFlagsCodeFix
                : MicrosoftCodeQualityAnalyzersResources.DoNotMarkEnumsWithFlagsCodeFix;

        private static async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            INamedTypeSymbol? flagsAttributeType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemFlagsAttribute);
            if (flagsAttributeType == null)
            {
                return;
            }

            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);
            if (diagnostic.Id == EnumWithFlagsAttributeAnalyzer.RuleIdMarkEnumsWithFlags)
            {
                SyntaxNode attribute = editor.Generator.Attribute(editor.Generator.TypeExpression(flagsAttributeType));
                editor.ReplaceNode(node, (currentNode, generator) => generator.AddAttributes(currentNode, attribute));
            }
            else if (model.GetDeclaredSymbol(node, cancellationToken) is INamedTypeSymbol enumType)
            {
                SyntaxNode attributeNode = enumType.GetAttribute(flagsAttributeType)!.ApplicationSyntaxReference!.GetSyntax(cancellationToken);
                editor.RemoveNode(attributeNode);
            }
        }
    }
}
