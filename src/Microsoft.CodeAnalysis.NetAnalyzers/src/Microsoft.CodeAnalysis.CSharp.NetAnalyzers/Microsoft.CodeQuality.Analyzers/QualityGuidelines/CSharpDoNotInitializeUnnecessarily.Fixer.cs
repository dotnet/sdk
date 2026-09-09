// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>CA1805: Do not initialize unnecessarily.</summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpDoNotInitializeUnnecessarilyFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DoNotInitializeUnnecessarilyAnalyzer.RuleId);

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            string title = MicrosoftCodeQualityAnalyzersResources.DoNotInitializeUnnecessarilyFix;
            RegisterCodeFix(context, title, title);
            return Task.CompletedTask;
        }

        protected sealed override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            // Get the target syntax node from the incoming span.  For a field like:
            //     private string _value = null;
            // the node will be for the `= null;` portion.  For a property like:
            //     private string Value { get; } = "hello";
            // the node will be for the `= "hello"`.
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);

            // Simply delete the field or property initializer.
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

            return Task.CompletedTask;
        }
    }
}