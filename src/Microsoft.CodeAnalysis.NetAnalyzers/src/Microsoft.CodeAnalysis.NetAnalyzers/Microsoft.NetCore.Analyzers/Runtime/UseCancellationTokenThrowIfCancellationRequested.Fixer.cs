// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
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
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic)]
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
            IOperation? whenTrue = UseCancellationTokenThrowIfCancellationRequested.GetSingleStatementOrDefault(conditional.WhenTrue);
            IOperation? whenFalse = UseCancellationTokenThrowIfCancellationRequested.GetSingleStatementOrDefault(conditional.WhenFalse);

            Func<CancellationToken, Task<Document>> createChangedDocument;
            if (symbols.IsSimpleAffirmativeCheck(conditional, out IPropertyReferenceOperation? propertyReference))
            {
                //  For simple checks of the form:
                //      if (token.IsCancellationRequested)
                //          throw new OperationCanceledException();
                //  Replace with:
                //      token.ThrowIfCancellationRequested();
                createChangedDocument = async token =>
                {
                    var editor = await DocumentEditor.CreateAsync(context.Document, token).ConfigureAwait(false);
                    SyntaxNode expressionStatement = CreateThrowIfCancellationRequestedExpressionStatement(editor, conditional, propertyReference);
                    editor.ReplaceNode(conditional.Syntax, expressionStatement);
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
                    SyntaxNode expressionStatement = CreateThrowIfCancellationRequestedExpressionStatement(editor, conditional, propertyReference);
                    editor.ReplaceNode(conditional.Syntax, expressionStatement);
                    if (conditional.WhenTrue is IBlockOperation block)
                    {
                        editor.InsertAfter(expressionStatement, block.Operations.Select(x => x.Syntax.WithAdditionalAnnotations(Formatter.Annotation)));
                    }
                    else
                    {
                        editor.InsertAfter(expressionStatement, conditional.WhenTrue.Syntax);
                    }

                    //  Ensure if-blocks with multiple statements maintain correct indentation.
                    return await Formatter.FormatAsync(editor.GetChangedDocument(), Formatter.Annotation, cancellationToken: token).ConfigureAwait(false);
                };
            }
            else
            {
                return;
            }

            var codeAction = CodeAction.Create(
                Resx.UseCancellationTokenThrowIfCancellationRequestedTitle,
                createChangedDocument,
                Resx.UseCancellationTokenThrowIfCancellationRequestedTitle);
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
            return editor.Generator.ExpressionStatement(invocation).WithTriviaFrom(conditional.Syntax);
        }
    }
}
