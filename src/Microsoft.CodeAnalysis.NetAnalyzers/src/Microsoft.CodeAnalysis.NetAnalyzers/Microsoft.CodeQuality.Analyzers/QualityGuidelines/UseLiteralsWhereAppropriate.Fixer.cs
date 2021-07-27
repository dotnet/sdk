// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA1802: Use literals where appropriate
    /// </summary>
    public abstract class UseLiteralsWhereAppropriateFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(UseLiteralsWhereAppropriateAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode declaration = root.FindNode(context.Span);
            declaration = SyntaxGenerator.GetGenerator(context.Document).GetDeclaration(declaration, DeclarationKind.Field);
            var fieldFeclaration = GetFieldDeclaration(declaration);
            if (fieldFeclaration == null)
            {
                return;
            }

            string title = MicrosoftCodeQualityAnalyzersResources.UseLiteralsWhereAppropriateCodeActionTitle;
            context.RegisterCodeFix(
                new MyCodeAction(
                    title,
                    cancellationToken => ToConstantDeclarationAsync(context.Document, fieldFeclaration, cancellationToken),
                    equivalenceKey: title),
                context.Diagnostics);
        }

        private async Task<Document> ToConstantDeclarationAsync(Document document, SyntaxNode fieldDeclaration, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

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
            return editor.GetChangedDocument();
        }

        protected abstract SyntaxNode? GetFieldDeclaration(SyntaxNode syntaxNode);
        protected abstract bool IsStaticKeyword(SyntaxToken syntaxToken);
        protected abstract bool IsReadonlyKeyword(SyntaxToken syntaxToken);
        protected abstract SyntaxToken GetConstKeywordToken();

        protected abstract SyntaxTokenList GetModifiers(SyntaxNode fieldSyntax);
        protected abstract SyntaxNode WithModifiers(SyntaxNode fieldSyntax, SyntaxTokenList modifiers);

        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }
    }
}