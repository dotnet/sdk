// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    using static MicrosoftCodeQualityAnalyzersResources;
    using RuleKind = UseCrossPlatformIntrinsicsAnalyzer.RuleKind;

    public abstract class UseCrossPlatformIntrinsicsFixer : OrderedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(UseCrossPlatformIntrinsicsAnalyzer.RuleId);

        protected sealed override string CodeActionTitle => UseCrossPlatformIntrinsicsTitle;

        protected sealed override string CodeActionEquivalenceKey => nameof(UseCrossPlatformIntrinsicsFixer);

        protected sealed override Task FixAllCoreAsync(SyntaxEditor editor, SyntaxGenerator generator, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            // We shouldn't get here for a diagnostic that doesn't have the expected properties.

            if (!diagnostic.Properties.TryGetValue(nameof(RuleKind), out string? ruleKindName))
            {
                Debug.Fail($"Found diagnostic without an associated {nameof(RuleKind)} property.");
                return Task.CompletedTask;
            }

            if (!Enum.TryParse(ruleKindName, out RuleKind ruleKind))
            {
                Debug.Fail($"Found diagnostic with an unrecognized {nameof(RuleKind)} property: {ruleKindName}.");
                return Task.CompletedTask;
            }

            editor.ReplaceNode(node, (currentNode, generator) => ReplaceNode(currentNode, generator, ruleKind));
            return Task.CompletedTask;
        }

        protected virtual SyntaxNode ReplaceNode(SyntaxNode currentNode, SyntaxGenerator generator, RuleKind ruleKind)
        {
            return ruleKind switch
            {
                RuleKind.op_Addition => ReplaceWithBinaryOperator(currentNode, generator, isCommutative: true, generator.AddExpression),
                RuleKind.op_BitwiseAnd => ReplaceWithBinaryOperator(currentNode, generator, isCommutative: true, generator.BitwiseAndExpression),
                RuleKind.op_BitwiseOr => ReplaceWithBinaryOperator(currentNode, generator, isCommutative: true, generator.BitwiseOrExpression),
                RuleKind.op_Division => ReplaceWithBinaryOperator(currentNode, generator, isCommutative: false, generator.DivideExpression),
                RuleKind.op_Multiply => ReplaceWithBinaryOperator(currentNode, generator, isCommutative: true, generator.MultiplyExpression),
                RuleKind.op_OnesComplement => ReplaceWithUnaryOperator(currentNode, generator, generator.BitwiseNotExpression),
                RuleKind.op_Subtraction => ReplaceWithBinaryOperator(currentNode, generator, isCommutative: false, generator.SubtractExpression),
                RuleKind.op_UnaryNegation => ReplaceWithUnaryOperator(currentNode, generator, generator.NegateExpression),
                _ => currentNode,
            };
        }

        protected abstract SyntaxNode ReplaceWithUnaryOperator(SyntaxNode currentNode, SyntaxGenerator generator, Func<SyntaxNode, SyntaxNode?> unaryOpFunc);

        protected abstract SyntaxNode ReplaceWithBinaryOperator(SyntaxNode currentNode, SyntaxGenerator generator, bool isCommutative, Func<SyntaxNode, SyntaxNode, SyntaxNode?> binaryOpFunc);
    }
}
