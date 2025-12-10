// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>CA1830: Prefer strongly-typed StringBuilder.Append overloads.</summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class PreferTypedStringBuilderAppendOverloadsFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(PreferTypedStringBuilderAppendOverloads.RuleId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            CancellationToken cancellationToken = context.CancellationToken;
            SyntaxNode root = await doc.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root.FindNode(context.Span) is SyntaxNode expression)
            {
                SemanticModel model = await doc.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                var operation = model.GetOperationWalkingUpParentChain(expression, cancellationToken);

                // Handle ToString() case
                if (operation is IArgumentOperation arg &&
                    arg.Value is IInvocationOperation invoke &&
                    invoke.Instance?.Syntax is SyntaxNode replacement)
                {
                    string title = MicrosoftNetCoreAnalyzersResources.PreferTypedStringBuilderAppendOverloadsRemoveToString;
                    context.RegisterCodeFix(
                        CodeAction.Create(title,
                            async ct =>
                            {
                                DocumentEditor editor = await DocumentEditor.CreateAsync(doc, ct).ConfigureAwait(false);
                                editor.ReplaceNode(expression, editor.Generator.Argument(replacement));
                                return editor.GetChangedDocument();
                            },
                            equivalenceKey: title),
                        context.Diagnostics);
                }
                // Handle new string(char, int) case (only for Append, not Insert)
                else if (operation is IArgumentOperation argOp &&
                    argOp.Value is IObjectCreationOperation objectCreation &&
                    objectCreation.Arguments.Length == 2 &&
                    argOp.Parent is IInvocationOperation invocationOp &&
                    invocationOp.TargetMethod.Name == "Append")
                {
                    string title = MicrosoftNetCoreAnalyzersResources.PreferTypedStringBuilderAppendOverloadsReplaceStringConstructor;
                    context.RegisterCodeFix(
                        CodeAction.Create(title,
                            async ct =>
                            {
                                DocumentEditor editor = await DocumentEditor.CreateAsync(doc, ct).ConfigureAwait(false);

                                // Get the char and int arguments from the string constructor
                                var charArgSyntax = objectCreation.Arguments[0].Value.Syntax;
                                var intArgSyntax = objectCreation.Arguments[1].Value.Syntax;

                                // Append(new string(c, count)) -> Append(c, count)
                                SyntaxNode newInvocation = editor.Generator.InvocationExpression(
                                    editor.Generator.MemberAccessExpression(
                                        invocationOp.Instance!.Syntax,
                                        "Append"),
                                    editor.Generator.Argument(charArgSyntax),
                                    editor.Generator.Argument(intArgSyntax));

                                editor.ReplaceNode(invocationOp.Syntax, newInvocation);
                                return editor.GetChangedDocument();
                            },
                            equivalenceKey: title),
                        context.Diagnostics);
                }
            }
        }
    }
}
