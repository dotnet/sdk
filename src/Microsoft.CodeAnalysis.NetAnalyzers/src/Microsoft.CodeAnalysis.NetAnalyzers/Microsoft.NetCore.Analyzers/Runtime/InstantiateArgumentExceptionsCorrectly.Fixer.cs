// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2208: Instantiate argument exceptions correctly
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class InstantiateArgumentExceptionsCorrectlyFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(InstantiateArgumentExceptionsCorrectlyAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            string paramPositionString = diagnostic.Properties.GetValueOrDefault(InstantiateArgumentExceptionsCorrectlyAnalyzer.MessagePosition);
            if (paramPositionString != null)
            {
                SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
                SyntaxNode node = root.FindNode(context.Span, getInnermostNodeForTie: true);
                if (node != null)
                {
                    await PopulateCodeFixAsync(context, diagnostic, paramPositionString, node).ConfigureAwait(false);
                }
            }
        }

        private static async Task PopulateCodeFixAsync(CodeFixContext context, Diagnostic diagnostic, string paramPositionString, SyntaxNode node)
        {
            SemanticModel model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var operation = model.GetOperation(node, context.CancellationToken);
            if (operation is IObjectCreationOperation creation)
            {
                if (int.TryParse(paramPositionString, out int paramPosition))
                {
                    CodeAction? codeAction = null;
                    if (creation.Arguments.Length == 1)
                    {
                        // Add null message
                        codeAction = CodeAction.Create(
                            title: MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyChangeToTwoArgumentCodeFixTitle,
                            createChangedDocument: c => AddNullMessageToArgumentListAsync(context.Document, creation, c),
                            equivalenceKey: MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyChangeToTwoArgumentCodeFixTitle);
                    }
                    else
                    {
                        // Swap message and parameter name
                        codeAction = CodeAction.Create(
                            title: MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyFlipArgumentOrderCodeFixTitle,
                            createChangedDocument: c => SwapArgumentsOrderAsync(context.Document, creation, paramPosition, creation.Arguments.Length, c),
                            equivalenceKey: MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyFlipArgumentOrderCodeFixTitle);
                    }
                    context.RegisterCodeFix(codeAction, diagnostic);
                }
            }
        }

        private static async Task<Document> SwapArgumentsOrderAsync(Document document, IObjectCreationOperation creation, int paramPosition, int argumentCount, CancellationToken token)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, token).ConfigureAwait(false);
            SyntaxNode parameter = AddNameOfIfLiteral(creation.Arguments[paramPosition].Value, editor.Generator);
            SyntaxNode newCreation;
            if (argumentCount == 2)
            {
                if (paramPosition == 0)
                {
                    newCreation = editor.Generator.ObjectCreationExpression(creation.Type, creation.Arguments[1].Syntax, parameter);
                }
                else
                {
                    newCreation = editor.Generator.ObjectCreationExpression(creation.Type, parameter, creation.Arguments[0].Syntax);
                }
            }
            else
            {
                Debug.Assert(argumentCount == 3);
                if (paramPosition == 0)
                {
                    newCreation = editor.Generator.ObjectCreationExpression(creation.Type, creation.Arguments[1].Syntax, parameter, creation.Arguments[2].Syntax);
                }
                else
                {
                    newCreation = editor.Generator.ObjectCreationExpression(creation.Type, parameter, creation.Arguments[1].Syntax, creation.Arguments[0].Syntax);
                }
            }
            editor.ReplaceNode(creation.Syntax, newCreation);
            return editor.GetChangedDocument();
        }

        private static async Task<Document> AddNullMessageToArgumentListAsync(Document document, IObjectCreationOperation creation, CancellationToken token)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, token).ConfigureAwait(false);
            SyntaxNode argument = AddNameOfIfLiteral(creation.Arguments[0].Value, editor.Generator);
            SyntaxNode newCreation = editor.Generator.ObjectCreationExpression(creation.Type, editor.Generator.Argument(editor.Generator.NullLiteralExpression()), argument);
            editor.ReplaceNode(creation.Syntax, newCreation);
            return editor.GetChangedDocument();
        }

        private static SyntaxNode AddNameOfIfLiteral(IOperation expression, SyntaxGenerator generator)
        {
            if (expression is ILiteralOperation literal)
            {
                return generator.NameOfExpression(generator.IdentifierName(literal.ConstantValue.Value.ToString()));
            }
            return expression.Syntax;
        }
    }
}