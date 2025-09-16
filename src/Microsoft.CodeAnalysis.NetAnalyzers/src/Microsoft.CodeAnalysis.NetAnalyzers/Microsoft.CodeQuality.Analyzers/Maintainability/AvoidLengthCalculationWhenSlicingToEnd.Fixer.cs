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

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    /// <summary>
    /// CA1514: <inheritdoc cref="MicrosoftCodeQualityAnalyzersResources.AvoidLengthCalculationWhenSlicingToEndTitle"/>
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class AvoidLengthCalculationWhenSlicingToEndFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(AvoidLengthCalculationWhenSlicingToEndAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span, getInnermostNodeForTie: true);

            if (node is null)
            {
                return;
            }

            var semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var operation = semanticModel.GetOperation(node, context.CancellationToken);

            if (operation is not IInvocationOperation invocationOperation ||
                invocationOperation.Instance is null ||
                invocationOperation.Arguments.Length != 2)
            {
                return;
            }

            var codeAction = CodeAction.Create(
                MicrosoftCodeQualityAnalyzersResources.AvoidLengthCalculationWhenSlicingToEndCodeFixTitle,
                ct => ReplaceWithStartOnlyCall(
                    context.Document,
                    invocationOperation.Instance.Syntax,
                    invocationOperation.TargetMethod.Name,
                    invocationOperation.Arguments.GetArgumentsInParameterOrder()[0],
                    ct),
                nameof(MicrosoftCodeQualityAnalyzersResources.AvoidLengthCalculationWhenSlicingToEndCodeFixTitle));

            context.RegisterCodeFix(codeAction, context.Diagnostics);

            async Task<Document> ReplaceWithStartOnlyCall(
                Document document,
                SyntaxNode instance,
                string methodName,
                IArgumentOperation argument,
                CancellationToken cancellationToken)
            {
                var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
                var generator = editor.Generator;
                var methodExpression = generator.MemberAccessExpression(instance, methodName);
                var methodInvocation = generator.InvocationExpression(methodExpression, argument.Syntax);

                editor.ReplaceNode(invocationOperation.Syntax, methodInvocation.WithTriviaFrom(invocationOperation.Syntax));

                return document.WithSyntaxRoot(editor.GetChangedRoot());
            }
        }
    }
}
