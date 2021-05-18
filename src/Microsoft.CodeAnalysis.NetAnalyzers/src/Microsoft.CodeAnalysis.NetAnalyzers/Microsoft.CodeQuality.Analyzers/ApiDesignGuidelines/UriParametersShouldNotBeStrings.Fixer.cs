// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1054: Uri parameters should not be strings
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public class UriParametersShouldNotBeStringsFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(UriParametersShouldNotBeStringsAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // Fixes all occurrences within Document, Project, or Solution
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var title = MicrosoftCodeQualityAnalyzersResources.UriParametersShouldNotBeStringsCodeFixTitle;

            var document = context.Document;
            var cancellationToken = context.CancellationToken;
            var span = context.Span;

            SemanticModel model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            INamedTypeSymbol? uriType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemUri);
            if (uriType == null)
            {
                return;
            }

            var generator = SyntaxGenerator.GetGenerator(document);

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var parameter = root.FindNode(span, getInnermostNodeForTie: true);
            if (parameter == null)
            {
                // this diagnostic is not something we can deal with
                return;
            }

            var methodNode = generator.GetDeclaration(parameter, DeclarationKind.Method);
            if (methodNode == null)
            {
                // this diagnostic is not something we can deal with
                return;
            }

            var targetNode = generator.GetDeclaration(parameter, DeclarationKind.Class) ?? generator.GetDeclaration(parameter, DeclarationKind.Struct);
            if (targetNode == null)
            {
                // this diagnostic is not something we can deal with
                return;
            }

            context.RegisterCodeFix(CodeAction.Create(title, c => AddMethodAsync(context.Document, context.Span, methodNode, targetNode, uriType, c), equivalenceKey: title), context.Diagnostics);
        }

        private static async Task<Document> AddMethodAsync(Document document, TextSpan span, SyntaxNode methodNode, SyntaxNode targetNode, INamedTypeSymbol uriType, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var generator = editor.Generator;

            var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var methodSymbol = (IMethodSymbol)model.GetDeclaredSymbol(methodNode, cancellationToken);

            var parameterIndex = GetParameterIndex(methodSymbol, model.SyntaxTree, span);
            if (parameterIndex < 0)
            {
                // this is not something we can handle
                return document;
            }

            var newMethod = CreateNewMethod(generator, methodSymbol, parameterIndex, editor.SemanticModel.Compilation, uriType);
            editor.AddMember(targetNode, newMethod);

            return editor.GetChangedDocument();
        }

        private static SyntaxNode CreateNewMethod(
            SyntaxGenerator generator, IMethodSymbol methodSymbol, int parameterIndex, Compilation compilation, INamedTypeSymbol uriType)
        {
            // create original parameter decl
            var originalParameter = generator.ParameterDeclaration(methodSymbol.Parameters[parameterIndex]);

            // replace original parameter type to System.Uri
            var newParameter = generator.ReplaceNode(originalParameter, generator.GetType(originalParameter), generator.TypeExpression(uriType));

            // create original method decl
            var original = generator.MethodDeclaration(methodSymbol, generator.DefaultMethodBody(compilation));

            // get parameters from original method decl
            var originalParameters = generator.GetParameters(original);

            // replace one of parameter to new one
            return generator.ReplaceNode(original, originalParameters[parameterIndex], newParameter);
        }

        private static int GetParameterIndex(IMethodSymbol methodSymbol, SyntaxTree tree, TextSpan span)
        {
            for (var i = 0; i < methodSymbol.Parameters.Length; i++)
            {
                var parameter = methodSymbol.Parameters[i];
                if (parameter.Locations.Any(l => l.IsInSource && l.SourceTree == tree && l.SourceSpan.IntersectsWith(span)))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}