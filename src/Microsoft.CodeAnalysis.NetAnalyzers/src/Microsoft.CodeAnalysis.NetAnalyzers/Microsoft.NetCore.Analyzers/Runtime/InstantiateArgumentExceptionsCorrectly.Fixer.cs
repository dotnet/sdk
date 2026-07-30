// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2208: Instantiate argument exceptions correctly
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class InstantiateArgumentExceptionsCorrectlyFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(InstantiateArgumentExceptionsCorrectlyAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            string? paramPositionString = diagnostic.Properties.GetValueOrDefault(InstantiateArgumentExceptionsCorrectlyAnalyzer.MessagePosition);
            if (paramPositionString != null)
            {
                SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
                SyntaxNode node = root.FindNode(context.Span, getInnermostNodeForTie: true);
                if (node != null)
                {
                    await PopulateCodeFixAsync(context, diagnostic, paramPositionString, node).ConfigureAwait(false);
                }
            }
        }

        private static async Task PopulateCodeFixAsync(CodeFixContext context, Diagnostic diagnostic, string paramPositionString, SyntaxNode node)
        {
            SemanticModel model = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
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
            SyntaxNode parameter = AddNameOfIfLiteral(creation.Arguments.GetArgumentForParameterAtIndex(paramPosition).Value, editor.Generator);
            SyntaxNode newCreation;
            if (argumentCount == 2)
            {
                if (paramPosition == 0)
                {
                    newCreation = editor.Generator.ObjectCreationExpression(creation.Type, ExpressionForParameter(creation, 1), parameter);
                }
                else
                {
                    newCreation = editor.Generator.ObjectCreationExpression(creation.Type, parameter, ExpressionForParameter(creation, 0));
                }
            }
            else
            {
                Debug.Assert(argumentCount == 3);
                if (paramPosition == 0)
                {
                    newCreation = editor.Generator.ObjectCreationExpression(creation.Type, ExpressionForParameter(creation, 1), parameter, ExpressionForParameter(creation, 2));
                }
                else
                {
                    newCreation = editor.Generator.ObjectCreationExpression(creation.Type, parameter, ExpressionForParameter(creation, 1), ExpressionForParameter(creation, 0));
                }
            }

            editor.ReplaceNode(creation.Syntax, newCreation);
            return editor.GetChangedDocument();
        }

        private static async Task<Document> AddNullMessageToArgumentListAsync(Document document, IObjectCreationOperation creation, CancellationToken token)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, token).ConfigureAwait(false);
            SyntaxNode argument = AddNameOfIfLiteral(creation.Arguments.GetArgumentForParameterAtIndex(0).Value, editor.Generator);
            SyntaxNode newCreation = editor.Generator.ObjectCreationExpression(creation.Type, editor.Generator.Argument(editor.Generator.NullLiteralExpression()), argument);
            editor.ReplaceNode(creation.Syntax, newCreation);
            return editor.GetChangedDocument();
        }

        /// <remarks>
        /// The rewritten argument list is positional, so the value is taken without the enclosing
        /// argument: carrying a named argument's syntax into a different position would either name
        /// the wrong parameter or fail to compile.
        /// </remarks>
        private static SyntaxNode ExpressionForParameter(IObjectCreationOperation creation, int parameterIndex)
            => creation.Arguments.GetArgumentForParameterAtIndex(parameterIndex).Value.Syntax;

        private static SyntaxNode AddNameOfIfLiteral(IOperation expression, SyntaxGenerator generator)
        {
            if (expression is ILiteralOperation literal &&
                literal.ConstantValue.Value is { } value)
            {
                return generator.NameOfExpression(generator.IdentifierName(value.ToString()));
            }

            return expression.Syntax;
        }
    }
}