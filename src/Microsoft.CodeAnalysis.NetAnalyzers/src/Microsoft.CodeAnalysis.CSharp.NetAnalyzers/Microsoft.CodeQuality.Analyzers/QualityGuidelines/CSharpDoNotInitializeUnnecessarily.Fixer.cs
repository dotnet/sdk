// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>CA1805: Do not initialize unnecessarily.</summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpDoNotInitializeUnnecessarilyFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DoNotInitializeUnnecessarilyAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            SyntaxNode root = await doc.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // Get the target syntax node from the incoming span.  For a field like:
            //     private string _value = null;
            // the node will be for the `= null;` portion.  For a property like:
            //     private string Value { get; } = "hello";
            // the node will be for the `= "hello"`.
            if (root.FindNode(context.Span) is SyntaxNode node)
            {
                string title = MicrosoftCodeQualityAnalyzersResources.DoNotInitializeUnnecessarilyFix;
                context.RegisterCodeFix(
                    CodeAction.Create(title,
                    async ct =>
                    {
                        // Simply delete the field or property initializer.
                        DocumentEditor editor = await DocumentEditor.CreateAsync(doc, ct).ConfigureAwait(false);
                        if (node.Parent is PropertyDeclarationSyntax prop)
                        {
                            // For a property, we also need to get rid of the semicolon that follows the initializer.
                            var newProp = prop.TrackNodes(node);
                            var newTrailingTrivia = newProp.Initializer!.GetTrailingTrivia()
                                                    .AddRange(newProp.SemicolonToken.LeadingTrivia)
                                                    .AddRange(newProp.SemicolonToken.TrailingTrivia);
                            newProp = newProp.WithSemicolonToken(default)
                                        .WithTrailingTrivia(newTrailingTrivia)
                                        .WithAdditionalAnnotations(Formatter.Annotation);

                            newProp = newProp.RemoveNode(newProp.GetCurrentNode(node)!, SyntaxRemoveOptions.KeepExteriorTrivia)!;
                            editor.ReplaceNode(prop, newProp);
                        }
                        else
                        {
                            editor.RemoveNode(node);
                        }

                        // Return the new doc.
                        return editor.GetChangedDocument();
                    },
                    equivalenceKey: title),
                    context.Diagnostics);
            }
        }
    }
}