// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class PreferStringContainsOverIndexOfFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(PreferStringContainsOverIndexOfAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            CancellationToken cancellationToken = context.CancellationToken;
            SyntaxNode root = await doc.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root.FindNode(context.Span) is not SyntaxNode expression)
            {
                return;
            }

            SemanticModel semanticModel = await doc.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var operation = semanticModel.GetOperation(expression, cancellationToken);

            // Not offering a code-fix for the variable declaration case
            if (operation is not IBinaryOperation binaryOperation)
            {
                return;
            }

            IInvocationOperation invocationOperation;
            IOperation otherOperation;
            if (binaryOperation.LeftOperand is IInvocationOperation invocationOperationOperand)
            {
                invocationOperation = invocationOperationOperand;
                otherOperation = binaryOperation.RightOperand;
            }
            else
            {
                invocationOperation = (IInvocationOperation)binaryOperation.RightOperand;
                otherOperation = binaryOperation.LeftOperand;
            }

            switch (invocationOperation.Arguments.Length)
            {
                case 1:
                case 2:
                    break;
                default:
                    return;
            }

            var instanceOperation = invocationOperation.Instance;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: MicrosoftNetCoreAnalyzersResources.PreferStringContainsOverIndexOfCodeFixTitle,
                    createChangedDocument: c => ReplaceBinaryOperationWithContains(doc, instanceOperation.Syntax, invocationOperation.Arguments, binaryOperation, c),
                    equivalenceKey: MicrosoftNetCoreAnalyzersResources.PreferStringContainsOverIndexOfCodeFixTitle),
                context.Diagnostics);
            return;

            async Task<Document?> ReplaceBinaryOperationWithContains(Document document, SyntaxNode syntaxNode, ImmutableArray<IArgumentOperation> indexOfMethodArguments, IBinaryOperation binaryOperation, CancellationToken cancellationToken)
            {
                DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
                SyntaxGenerator generator = editor.Generator;
                var containsExpression = generator.MemberAccessExpression(syntaxNode, "Contains");
                SyntaxNode? containsInvocation = null;
                int numberOfArguments = indexOfMethodArguments.Length;
                if (numberOfArguments == 1)
                {
                    var firstArgument = indexOfMethodArguments[0];
                    if (firstArgument.Parameter.Type.SpecialType == SpecialType.System_Char)
                    {
                        containsInvocation = generator.InvocationExpression(containsExpression, firstArgument.Syntax);
                    }
                    else
                    {
                        var systemNode = generator.IdentifierName("System");
                        var argument = generator.MemberAccessExpression(generator.MemberAccessExpression(systemNode, "StringComparison"), "CurrentCulture");
                        containsInvocation = generator.InvocationExpression(containsExpression, firstArgument.Syntax, argument);
                    }
                }
                else
                {
                    int stringOrCharArgumentIndex, ordinalArgumentIndex;
                    if (indexOfMethodArguments[0].Value.Type.SpecialType is SpecialType.System_String or SpecialType.System_Char)
                    {
                        stringOrCharArgumentIndex = 0;
                        ordinalArgumentIndex = 1;
                    }
                    else
                    {
                        stringOrCharArgumentIndex = 1;
                        ordinalArgumentIndex = 0;
                    }

                    var ordinalArgumentValue = indexOfMethodArguments[ordinalArgumentIndex].Value;
                    if (ordinalArgumentValue.ConstantValue.HasValue &&
                        ordinalArgumentValue.ConstantValue.Value is int intValue &&
                        (StringComparison)intValue == StringComparison.Ordinal)
                    {
                        containsInvocation = generator.InvocationExpression(containsExpression, indexOfMethodArguments[stringOrCharArgumentIndex].Syntax);
                    }
                    else
                    {
                        containsInvocation = generator.InvocationExpression(containsExpression, indexOfMethodArguments[0].Syntax, indexOfMethodArguments[1].Syntax);
                    }
                }

                SyntaxNode newIfCondition = (int)otherOperation.ConstantValue.Value != -1 ?
                                            containsInvocation :
                                            generator.LogicalNotExpression(containsInvocation);
                newIfCondition = newIfCondition.WithTriviaFrom(binaryOperation.Syntax);
                editor.ReplaceNode(binaryOperation.Syntax, newIfCondition);
                var newRoot = editor.GetChangedRoot();
                return document.WithSyntaxRoot(newRoot);
            }
        }
    }
}
