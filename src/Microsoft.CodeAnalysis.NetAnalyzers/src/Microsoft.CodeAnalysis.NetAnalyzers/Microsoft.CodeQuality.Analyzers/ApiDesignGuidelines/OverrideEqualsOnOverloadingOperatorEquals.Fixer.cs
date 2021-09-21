// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA2224: Override Equals on overloading operator equals
    /// </summary>
    public abstract class OverrideEqualsOnOverloadingOperatorEqualsFixer : CodeFixProvider
    {
        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode typeDeclaration = root.FindNode(context.Span);
            typeDeclaration = SyntaxGenerator.GetGenerator(context.Document).GetDeclaration(typeDeclaration);
            if (typeDeclaration == null)
            {
                return;
            }

            SemanticModel model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var typeSymbol = model.GetDeclaredSymbol(typeDeclaration, context.CancellationToken) as INamedTypeSymbol;
            if (typeSymbol?.TypeKind is not TypeKind.Class and
                not TypeKind.Struct)
            {
                return;
            }

            // CONSIDER: Do we need to confirm that System.Object.Equals isn't shadowed in a base type?

            string title = MicrosoftCodeQualityAnalyzersResources.OverrideEqualsOnOverloadingOperatorEqualsCodeActionTitle;
            context.RegisterCodeFix(
                new MyCodeAction(
                    title,
                    cancellationToken => OverrideObjectEqualsAsync(context.Document, typeDeclaration, typeSymbol, cancellationToken),
                    equivalenceKey: title),
                context.Diagnostics);
        }

        private static async Task<Document> OverrideObjectEqualsAsync(Document document, SyntaxNode typeDeclaration, INamedTypeSymbol typeSymbol, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var generator = editor.Generator;

            var methodDeclaration = generator.DefaultEqualsOverrideDeclaration(editor.SemanticModel.Compilation, typeSymbol);

            editor.AddMember(typeDeclaration, methodDeclaration);
            return editor.GetChangedDocument();
        }

        // Needed for Telemetry (https://github.com/dotnet/roslyn-analyzers/issues/192)
        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }
    }
}