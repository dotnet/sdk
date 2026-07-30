// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA1802: Use literals where appropriate
    /// </summary>
    public abstract class UseLiteralsWhereAppropriateFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(UseLiteralsWhereAppropriateAnalyzer.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode declaration = root.FindNode(context.Span);
            declaration = SyntaxGenerator.GetGenerator(context.Document).GetDeclaration(declaration, DeclarationKind.Field);
            if (GetFieldDeclaration(declaration) == null)
            {
                return;
            }

            string title = MicrosoftCodeQualityAnalyzersResources.UseLiteralsWhereAppropriateCodeActionTitle;
            RegisterCodeFix(context, title, title);
        }

        protected sealed override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SyntaxNode declaration = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);
            declaration = editor.Generator.GetDeclaration(declaration, DeclarationKind.Field);
            var fieldDeclaration = GetFieldDeclaration(declaration);
            if (fieldDeclaration == null)
            {
                return Task.CompletedTask;
            }

            SyntaxTriviaList leadingTrivia = new SyntaxTriviaList();
            SyntaxTriviaList trailingTrivia = new SyntaxTriviaList();

            SyntaxTokenList newModifiers = new SyntaxTokenList();
            foreach (SyntaxToken modifier in GetModifiers(fieldDeclaration))
            {
                if (IsStaticKeyword(modifier) || IsReadonlyKeyword(modifier))
                {
                    // The associated analyzer ensures we'll only get in the fixer if both 'static' and 'readonly'
                    // keywords are in the declaration. Because their order is not relevant, we detect if both
                    // have been passed by inspecting whether leading and trailing trivia are non-empty. 
                    if (leadingTrivia.Count == 0 && trailingTrivia.Count == 0)
                    {
                        leadingTrivia = leadingTrivia.AddRange(modifier.LeadingTrivia);
                        trailingTrivia = trailingTrivia.AddRange(modifier.TrailingTrivia);
                    }
                    else
                    {
                        // Copy the trivia in-between both keywords ('static' and 'readonly') into 
                        // the combined set of trailing trivia.
                        trailingTrivia = trailingTrivia.AddRange(modifier.LeadingTrivia);
                        trailingTrivia = trailingTrivia.AddRange(modifier.TrailingTrivia);

                        // We have processed both the keywords 'static' and 'readonly', so we insert the 'const' keyword here.
                        // In case any additional modifiers will follow, their relative position should not change.
                        SyntaxToken constModifier =
                            GetConstKeywordToken().WithLeadingTrivia(leadingTrivia).WithTrailingTrivia(trailingTrivia);
                        newModifiers = newModifiers.Add(constModifier);
                    }
                }
                else
                {
                    newModifiers = newModifiers.Add(modifier);
                }
            }

            var constFieldDeclaration = WithModifiers(fieldDeclaration, newModifiers).WithAdditionalAnnotations(Formatter.Annotation);
            editor.ReplaceNode(fieldDeclaration, constFieldDeclaration);
            return Task.CompletedTask;
        }

        protected abstract SyntaxNode? GetFieldDeclaration(SyntaxNode syntaxNode);
        protected abstract bool IsStaticKeyword(SyntaxToken syntaxToken);
        protected abstract bool IsReadonlyKeyword(SyntaxToken syntaxToken);
        protected abstract SyntaxToken GetConstKeywordToken();

        protected abstract SyntaxTokenList GetModifiers(SyntaxNode fieldSyntax);
        protected abstract SyntaxNode WithModifiers(SyntaxNode fieldSyntax, SyntaxTokenList modifiers);
    }
}