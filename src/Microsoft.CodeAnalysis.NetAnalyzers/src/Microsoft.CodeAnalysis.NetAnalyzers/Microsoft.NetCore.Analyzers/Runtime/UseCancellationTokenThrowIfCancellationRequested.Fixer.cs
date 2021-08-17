// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Operations;
using RequiredSymbols = Microsoft.NetCore.Analyzers.Runtime.UseCancellationTokenThrowIfCancellationRequested.RequiredSymbols;
using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// Use <see cref="CancellationToken.ThrowIfCancellationRequested"/> instead of checking <see cref="CancellationToken.IsCancellationRequested"/> and
    /// throwing <see cref="OperationCanceledException"/>.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class UseCancellationTokenThrowIfCancellationRequestedFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(UseCancellationTokenThrowIfCancellationRequested.RuleId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SemanticModel model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (!RequiredSymbols.TryGetSymbols(model.Compilation, out RequiredSymbols symbols))
                return;
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(context.Span);
            if (model.GetOperation(node, context.CancellationToken) is not IConditionalOperation conditional)
                return;

            Func<CancellationToken, Task<Document>> createChangedDocument;
            if (symbols.IsSimpleAffirmativeCheck(conditional, out IPropertyReferenceOperation? propertyReference))
            {
                //  For simple checks of the form:
                //      if (token.IsCancellationRequested)
                //          throw new OperationCanceledException();
                //  Replace with:
                //      token.ThrowIfCancellationRequested();
                //
                //  For simple checks of the form:
                //      if (token.IsCancellationRequested)
                //          throw new OperationCanceledException();
                //      else
                //          Frob();
                //  Replace with:
                //      token.ThrowIfCancellationRequested();
                //      Frob();
                createChangedDocument = async token =>
                {
                    var editor = await DocumentEditor.CreateAsync(context.Document, token).ConfigureAwait(false);
                    SyntaxNode expressionStatement = CreateThrowIfCancellationRequestedExpressionStatement(editor, conditional, propertyReference);
                    editor.ReplaceNode(conditional.Syntax, expressionStatement);

                    if (conditional.WhenFalse is IBlockOperation block)
                    {
                        editor.InsertAfter(expressionStatement, block.Operations.Select(x => x.Syntax.WithAdditionalAnnotations(Formatter.Annotation)));
                    }
                    else if (conditional.WhenFalse is not null)
                    {
                        editor.InsertAfter(expressionStatement, conditional.WhenFalse.Syntax);
                    }

                    return editor.GetChangedDocument();
                };
            }
            else if (symbols.IsNegatedCheckWithThrowingElseClause(conditional, out propertyReference))
            {
                //  For negated checks of the form:
                //      if (!token.IsCancellationRequested) { DoStatements(); }
                //      else { throw new OperationCanceledException(); }
                //  Replace with:
                //      token.ThrowIfCancellationRequested();
                //      DoStatements();
                createChangedDocument = async token =>
                {
                    var editor = await DocumentEditor.CreateAsync(context.Document, token).ConfigureAwait(false);

                    SyntaxNode expressionStatement = CreateThrowIfCancellationRequestedExpressionStatement(editor, conditional, propertyReference)
                        .WithAdditionalAnnotations(Formatter.Annotation);
                    editor.ReplaceNode(conditional.Syntax, expressionStatement);

                    if (conditional.WhenTrue is IBlockOperation block)
                    {
                        editor.InsertAfter(expressionStatement, block.Operations.Select(x => x.Syntax.WithAdditionalAnnotations(Formatter.Annotation)));
                    }
                    else
                    {
                        editor.InsertAfter(expressionStatement, conditional.WhenTrue.Syntax);
                    }

                    return editor.GetChangedDocument();
                };
            }
            else
            {
                return;
            }

            var codeAction = CodeAction.Create(
                Resx.UseCancellationTokenThrowIfCancellationRequestedCodeFixTitle,
                createChangedDocument,
                Resx.UseCancellationTokenThrowIfCancellationRequestedCodeFixTitle);
            context.RegisterCodeFix(codeAction, context.Diagnostics);
        }

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        private static SyntaxNode CreateThrowIfCancellationRequestedExpressionStatement(
            DocumentEditor editor,
            IConditionalOperation conditional,
            IPropertyReferenceOperation isCancellationRequestedPropertyReference)
        {
            SyntaxNode memberAccess = editor.Generator.MemberAccessExpression(
                isCancellationRequestedPropertyReference.Instance.Syntax,
                nameof(CancellationToken.ThrowIfCancellationRequested));
            SyntaxNode invocation = editor.Generator.InvocationExpression(memberAccess, Array.Empty<SyntaxNode>());
            var firstWhenTrueStatement = conditional.WhenTrue is IBlockOperation block ? block.Operations.FirstOrDefault() : conditional.WhenTrue;

            var result = editor.Generator.ExpressionStatement(invocation);
            result = firstWhenTrueStatement is not null ? result.WithTriviaFrom(firstWhenTrueStatement.Syntax) : result;
            return result.WithAdditionalAnnotations(Formatter.Annotation);
        }
    }
}
