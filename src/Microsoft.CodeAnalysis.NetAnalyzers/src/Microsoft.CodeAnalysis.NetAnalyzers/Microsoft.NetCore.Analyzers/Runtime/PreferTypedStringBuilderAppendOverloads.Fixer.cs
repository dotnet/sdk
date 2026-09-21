// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>CA1830: Prefer strongly-typed StringBuilder.Append overloads.</summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class PreferTypedStringBuilderAppendOverloadsFixer : CodeFixProvider
    {
        private static readonly string s_removeToStringTitle = MicrosoftNetCoreAnalyzersResources.PreferTypedStringBuilderAppendOverloadsRemoveToString;
        private static readonly string s_replaceStringConstructorTitle = MicrosoftNetCoreAnalyzersResources.PreferTypedStringBuilderAppendOverloadsReplaceStringConstructor;

        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(PreferTypedStringBuilderAppendOverloads.RuleId);

        // The two shapes carry different fix titles, and so different equivalence keys, which
        // SyntaxEditorFixAllProvider does not filter on - so the state is the key to apply.
        public sealed override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(context => context.CodeActionEquivalenceKey, ApplyFixAsync);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            CancellationToken cancellationToken = context.CancellationToken;
            SyntaxNode root = await doc.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            SemanticModel model = await doc.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (GetTitle(model, root.FindNode(context.Span), cancellationToken) is not string title)
            {
                return;
            }

            ImmutableArray<Diagnostic> diagnostics = context.Diagnostics;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken => SyntaxEditorFixAllProvider.ApplyFixesAsync(
                        doc,
                        diagnostics,
                        (document, diagnostic, editor, token) => ApplyFixAsync(document, diagnostic, editor, title, token),
                        cancellationToken),
                    equivalenceKey: title),
                diagnostics);
        }

        private static async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, string? equivalenceKey, CancellationToken cancellationToken)
        {
            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode expression = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);
            IOperation? operation = model.GetOperationWalkingUpParentChain(expression, cancellationToken);

            if (GetTitle(operation) is not string title ||
                (equivalenceKey is not null && title != equivalenceKey))
            {
                return;
            }

            // Handle ToString() case
            if (title == s_removeToStringTitle)
            {
                SyntaxNode replacement = ((IInvocationOperation)((IArgumentOperation)operation!).Value).Instance!.Syntax;

                editor.TrackNode(replacement);
                editor.ReplaceNode(expression, (currentNode, generator) => generator.Argument(currentNode.GetCurrentNode(replacement) ?? replacement));
            }
            // Handle new string(char, int) case (only for Append, not Insert)
            else
            {
                var argOp = (IArgumentOperation)operation!;
                var objectCreation = (IObjectCreationOperation)argOp.Value;
                var invocationOp = (IInvocationOperation)argOp.Parent!;

                // Get the char and int arguments from the string constructor
                SyntaxNode instance = invocationOp.Instance!.Syntax;
                SyntaxNode charArgSyntax = objectCreation.Arguments[0].Value.Syntax;
                SyntaxNode intArgSyntax = objectCreation.Arguments[1].Value.Syntax;

                editor.TrackNode(instance);
                editor.TrackNode(charArgSyntax);
                editor.TrackNode(intArgSyntax);

                // Append(new string(c, count)) -> Append(c, count)
                editor.ReplaceNode(invocationOp.Syntax, (currentNode, generator) =>
                {
                    SyntaxNode Current(SyntaxNode original) => currentNode.GetCurrentNode(original) ?? original;

                    return generator.InvocationExpression(
                        generator.MemberAccessExpression(Current(instance), "Append"),
                        generator.Argument(Current(charArgSyntax)),
                        generator.Argument(Current(intArgSyntax)));
                });
            }
        }

        private static string? GetTitle(SemanticModel model, SyntaxNode? expression, CancellationToken cancellationToken)
            => expression is null ? null : GetTitle(model.GetOperationWalkingUpParentChain(expression, cancellationToken));

        private static string? GetTitle(IOperation? operation)
        {
            if (operation is not IArgumentOperation argument)
            {
                return null;
            }

            if (argument.Value is IInvocationOperation invoke && invoke.Instance is not null)
            {
                return s_removeToStringTitle;
            }

            return argument.Value is IObjectCreationOperation objectCreation &&
                objectCreation.Arguments.Length == 2 &&
                argument.Parent is IInvocationOperation invocationOp &&
                invocationOp.TargetMethod.Name == "Append" &&
                invocationOp.Instance is not null
                ? s_replaceStringConstructorTitle
                : null;
        }
    }
}
