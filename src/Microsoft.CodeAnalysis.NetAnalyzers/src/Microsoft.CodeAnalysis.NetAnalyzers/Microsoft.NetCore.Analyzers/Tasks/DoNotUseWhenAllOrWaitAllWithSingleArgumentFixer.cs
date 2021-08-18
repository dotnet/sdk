// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Tasks
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class DoNotUseWhenAllOrWaitAllWithSingleArgumentFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
            DoNotUseWhenAllOrWaitAllWithSingleArgument.WaitAllRule.Id,
            DoNotUseWhenAllOrWaitAllWithSingleArgument.WhenAllRule.Id);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var cancellationToken = context.CancellationToken;
            var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var nodeToFix = root.FindNode(context.Span, getInnermostNodeForTie: true);
            if (nodeToFix is null)
            {
                return;
            }

            var semanticModel = await context.Document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (semanticModel.GetOperation(nodeToFix, cancellationToken) is not IInvocationOperation operation)
            {
                return;
            }

            if (operation.TargetMethod.Name == nameof(Task.WaitAll))
            {
                var title = MicrosoftNetCoreAnalyzersResources.DoNotUseWaitAllWithSingleTaskFix;
                context.RegisterCodeFix(new MyCodeAction(title,
                    async ct =>
                    {
                        var editor = await DocumentEditor.CreateAsync(context.Document, ct).ConfigureAwait(false);
                        editor.ReplaceNode(nodeToFix,
                            editor.Generator.InvocationExpression(
                                editor.Generator.MemberAccessExpression(
                                    GetSingleArgumentSyntax(operation),
                                    nameof(Task.Wait))
                                ).WithTriviaFrom(nodeToFix)
                            );

                        return editor.GetChangedDocument();
                    },
                    equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseWaitAllWithSingleTaskFix)),
                context.Diagnostics);
            }
            else if (!IsValueStored(operation) && operation.TargetMethod.Name == nameof(Task.WhenAll))
            {
                var title = MicrosoftNetCoreAnalyzersResources.DoNotUseWhenAllWithSingleTaskFix;
                context.RegisterCodeFix(new MyCodeAction(title,
                    async ct =>
                    {
                        var editor = await DocumentEditor.CreateAsync(context.Document, ct).ConfigureAwait(false);
                        var newNode = GetSingleArgumentSyntax(operation).WithTriviaFrom(nodeToFix);

                        // The original invocation already returns a Task,
                        // so we can just replace directly with the argument
                        editor.ReplaceNode(nodeToFix, newNode);

                        return editor.GetChangedDocument();
                    },
                    equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseWhenAllWithSingleTaskFix)),
                context.Diagnostics);
            }
        }

        /// <summary>
        /// Returns true if the invocation is part of an assignment or variable declaration
        /// </summary>
        private static bool IsValueStored(IInvocationOperation operation)
        {
            var currentOperation = operation.Parent;
            while (currentOperation is not null)
            {
                if (currentOperation is IAssignmentOperation or
                    IVariableDeclarationOperation)
                {
                    return true;
                }

                currentOperation = currentOperation.Parent;
            }

            return false;
        }

        private static SyntaxNode GetSingleArgumentSyntax(IInvocationOperation operation)
            => ((IArrayCreationOperation)operation.Arguments[0].Value).Initializer.ElementValues[0].Syntax;

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        // Needed for Telemetry (https://github.com/dotnet/roslyn-analyzers/issues/192)
        private sealed class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey) :
                base(title, createChangedDocument, equivalenceKey)
            {
            }
        }
    }
}
