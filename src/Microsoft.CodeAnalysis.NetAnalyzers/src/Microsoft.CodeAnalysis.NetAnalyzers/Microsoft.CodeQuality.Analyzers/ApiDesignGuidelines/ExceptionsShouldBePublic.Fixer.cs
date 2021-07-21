// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.CodeActions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1064: Exceptions should be public
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class ExceptionsShouldBePublicFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ExceptionsShouldBePublicAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(context.Span);

            // create one equivalence key value for all actions produced by this fixer 
            // i.e. Fix All fixes every occurrence of this diagnostic
            string equivalenceKey = nameof(ExceptionsShouldBePublicFixer);

            CodeAction action = CodeAction.Create(
                MicrosoftCodeQualityAnalyzersResources.MakeExceptionPublic,
                c => MakePublic(context.Document, node, context.CancellationToken),
                equivalenceKey);

            context.RegisterCodeFix(action, context.Diagnostics);
        }

        private static async Task<Document> MakePublic(Document document, SyntaxNode classDecl, CancellationToken cancellationToken)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            editor.SetAccessibility(classDecl, Accessibility.Public);

            return editor.GetChangedDocument();
        }
    }
}