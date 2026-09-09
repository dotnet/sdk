// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class PreferStringContainsOverIndexOfFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(PreferStringContainsOverIndexOfAnalyzer.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            CancellationToken cancellationToken = context.CancellationToken;
            SyntaxNode root = await doc.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            SemanticModel semanticModel = await doc.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (GetIndexOfComparison(semanticModel, root.FindNode(context.Span), cancellationToken) is null)
            {
                return;
            }

            RegisterCodeFix(
                context,
                MicrosoftNetCoreAnalyzersResources.PreferStringContainsOverIndexOfCodeFixTitle,
                nameof(MicrosoftNetCoreAnalyzersResources.PreferStringContainsOverIndexOfCodeFixTitle));
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode expression = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);

            if (GetIndexOfComparison(semanticModel, expression, cancellationToken) is not (IBinaryOperation binaryOperation, IInvocationOperation invocationOperation, IOperation otherOperation))
            {
                return;
            }

            ImmutableArray<IArgumentOperation> indexOfMethodArguments = invocationOperation.Arguments;
            SyntaxNode instance = invocationOperation.Instance!.Syntax;

            bool negate = binaryOperation.OperatorKind == BinaryOperatorKind.Equals && (int)otherOperation.ConstantValue.Value! == -1;
            ImmutableArray<SyntaxNode> carriedOver = ImmutableArray.Create(instance).AddRange(indexOfMethodArguments.Select(argument => argument.Syntax));

            foreach (SyntaxNode node in carriedOver)
            {
                editor.TrackNode(node);
            }

            // The receiver and the arguments are carried over from inside the node being replaced, so they have
            // to be read back off the current node rather than off the original tree.
            editor.ReplaceNode(binaryOperation.Syntax, (currentNode, generator) =>
            {
                SyntaxNode Current(SyntaxNode original) => currentNode.GetCurrentNode(original) ?? original;

                SyntaxNode containsExpression = generator.MemberAccessExpression(Current(instance), "Contains");
                SyntaxNode containsInvocation;

                if (indexOfMethodArguments.Length == 1)
                {
                    IArgumentOperation firstArgument = indexOfMethodArguments[0];
                    if (firstArgument.Parameter?.Type.SpecialType == SpecialType.System_Char)
                    {
                        containsInvocation = generator.InvocationExpression(containsExpression, Current(firstArgument.Syntax));
                    }
                    else
                    {
                        SyntaxNode systemNode = generator.IdentifierName("System");
                        SyntaxNode argument = generator.MemberAccessExpression(generator.MemberAccessExpression(systemNode, "StringComparison"), "CurrentCulture");
                        containsInvocation = generator.InvocationExpression(containsExpression, Current(firstArgument.Syntax), argument);
                    }
                }
                else
                {
                    int stringOrCharArgumentIndex, ordinalArgumentIndex;
                    if (indexOfMethodArguments[0].Value.Type?.SpecialType is SpecialType.System_String or SpecialType.System_Char)
                    {
                        stringOrCharArgumentIndex = 0;
                        ordinalArgumentIndex = 1;
                    }
                    else
                    {
                        stringOrCharArgumentIndex = 1;
                        ordinalArgumentIndex = 0;
                    }

                    IOperation ordinalArgumentValue = indexOfMethodArguments[ordinalArgumentIndex].Value;
                    if (ordinalArgumentValue.ConstantValue.HasValue &&
                        ordinalArgumentValue.ConstantValue.Value is int intValue &&
                        (StringComparison)intValue == StringComparison.Ordinal)
                    {
                        containsInvocation = generator.InvocationExpression(containsExpression, Current(indexOfMethodArguments[stringOrCharArgumentIndex].Syntax));
                    }
                    else
                    {
                        containsInvocation = generator.InvocationExpression(containsExpression, Current(indexOfMethodArguments[0].Syntax), Current(indexOfMethodArguments[1].Syntax));
                    }
                }

                // We first check for "IndexOf() == -1" which translates to "!Contains()". All other covered cases do not need negation.
                SyntaxNode newIfCondition = negate ? generator.LogicalNotExpression(containsInvocation) : containsInvocation;
                return newIfCondition.WithTriviaFrom(currentNode);
            });
        }

        private static (IBinaryOperation Comparison, IInvocationOperation IndexOf, IOperation Other)? GetIndexOfComparison(
            SemanticModel semanticModel, SyntaxNode? expression, CancellationToken cancellationToken)
        {
            // Not offering a code-fix for the variable declaration case
            if (expression is null ||
                semanticModel.GetOperation(expression, cancellationToken) is not IBinaryOperation binaryOperation)
            {
                return null;
            }

            IInvocationOperation invocationOperation;
            IOperation otherOperation;
            if (binaryOperation.LeftOperand is IInvocationOperation invocationOperationOperand)
            {
                invocationOperation = invocationOperationOperand;
                otherOperation = binaryOperation.RightOperand;
            }
            else if (binaryOperation.RightOperand is IInvocationOperation rightInvocationOperation)
            {
                invocationOperation = rightInvocationOperation;
                otherOperation = binaryOperation.LeftOperand;
            }
            else
            {
                return null;
            }

            return invocationOperation.Arguments.Length is 1 or 2 && invocationOperation.Instance is not null
                ? (binaryOperation, invocationOperation, otherOperation)
                : null;
        }
    }
}
