// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;
using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;
using RequiredSymbols = Microsoft.NetCore.Analyzers.Runtime.UseSpanBasedStringConcat.RequiredSymbols;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    public abstract class UseSpanBasedStringConcatFixer : SyntaxEditorBasedCodeFixProvider
    {
        private protected const string AsSpanName = nameof(MemoryExtensions.AsSpan);
        private protected const string AsSpanStartParameterName = "start";
        private protected const string ToStringName = nameof(ToString);
        private static readonly SyntaxAnnotation s_asSpanSymbolAnnotation = new("SymbolId", "System.MemoryExtensions");

        private protected abstract SyntaxNode ReplaceInvocationMethodName(SyntaxGenerator generator, SyntaxNode invocationSyntax, string newName);

        private protected abstract IOperation WalkDownBuiltInImplicitConversionOnConcatOperand(IOperation operand);

        private protected abstract bool IsNamedArgument(IArgumentOperation argumentOperation);

        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(UseSpanBasedStringConcat.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var model = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var concatExpressionSyntax = root.FindNode(context.Span, getInnermostNodeForTie: true);

            if (TryGetConcatOperands(model, concatExpressionSyntax, context.CancellationToken, out _, out _, out _))
            {
                RegisterCodeFix(context, Resx.UseSpanBasedStringConcatCodeFixTitle, nameof(Resx.UseSpanBasedStringConcatCodeFixTitle));
            }
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var concatExpressionSyntax = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            if (!TryGetConcatOperands(model, concatExpressionSyntax, cancellationToken, out var symbols, out var operands, out var roscharConcatMethod))
            {
                return;
            }

            //  Every argument is carried over from inside the node being replaced, so an operand that encloses a
            //  violation already fixed in this pass has to be read back off the current node, not the original tree.
            foreach (var operand in operands)
            {
                editor.TrackNode(operand.Syntax);

                var value = WalkDownBuiltInImplicitConversionOnConcatOperand(operand);
                editor.TrackNode(value.Syntax);

                if (value is IInvocationOperation invocation &&
                    symbols.IsAnySubstringMethod(invocation.TargetMethod) &&
                    TryGetNamedStartIndexArgument(symbols, invocation, out var namedStartIndexArgument))
                {
                    editor.TrackNode(namedStartIndexArgument.Syntax);
                    editor.TrackNode(namedStartIndexArgument.Value.Syntax);
                }
            }

            var capturedSymbols = symbols;

            editor.ReplaceNode(concatExpressionSyntax, (currentNode, generator) =>
            {
                SyntaxNode Current(SyntaxNode original) => currentNode.GetCurrentNode(original) ?? original;

                SyntaxNode stringTypeNameSyntax = generator.TypeExpressionForStaticMemberAccess(capturedSymbols.StringType);
                SyntaxNode concatMemberAccessSyntax = generator.MemberAccessExpression(stringTypeNameSyntax, roscharConcatMethod.Name);

                //  Save leading and trailing trivia so it can be attached to the outside of the string.Concat invocation node.
                var leadingTrivia = Current(operands.First().Syntax).GetLeadingTrivia();
                var trailingTrivia = Current(operands.Last().Syntax).GetTrailingTrivia();

                var arguments = ImmutableArray.CreateBuilder<SyntaxNode>(operands.Length);
                foreach (var operand in operands)
                    arguments.Add(ConvertOperandToArgument(capturedSymbols, generator, operand, Current));

                //  Strip off leading and trailing trivia from first and last operand nodes, respectively, and
                //  reattach it to the outside of the newly-created string.Concat invocation node.
                arguments[0] = arguments[0].WithoutLeadingTrivia();
                arguments[^1] = arguments[^1].WithoutTrailingTrivia();

                return generator.InvocationExpression(concatMemberAccessSyntax, arguments.MoveToImmutable())
                    .WithLeadingTrivia(leadingTrivia)
                    .WithTrailingTrivia(trailingTrivia);
            });
        }

        private bool TryGetConcatOperands(
            SemanticModel model,
            SyntaxNode concatExpressionSyntax,
            CancellationToken cancellationToken,
            out RequiredSymbols symbols,
            out ImmutableArray<IOperation> operands,
            [NotNullWhen(true)] out IMethodSymbol? roscharConcatMethod)
        {
            operands = ImmutableArray<IOperation>.Empty;
            roscharConcatMethod = null;

            if (!RequiredSymbols.TryGetSymbols(model.Compilation, out symbols))
            {
                return false;
            }

            //  OperatorKind will be BinaryOperatorKind.Concatenate, even when '+' is used instead of '&' in Visual Basic.
            if (model.GetOperation(concatExpressionSyntax, cancellationToken) is not IBinaryOperation concatOperation ||
                concatOperation.OperatorKind is not (BinaryOperatorKind.Add or BinaryOperatorKind.Concatenate))
            {
                return false;
            }

            operands = UseSpanBasedStringConcat.FlattenBinaryOperation(concatOperation);

            //  Bail out if we don't have a long enough span-based string.Concat overload.
            if (!symbols.TryGetRoscharConcatMethodWithArity(operands.Length, out roscharConcatMethod))
            {
                return false;
            }

            //  Bail if none of the operands are a non-conditional substring invocation. This could be the case if the
            //  only substring invocations in the expression were conditional invocations.
            foreach (var operand in operands)
            {
                if (WalkDownBuiltInImplicitConversionOnConcatOperand(operand) is IInvocationOperation invocation &&
                    symbols.IsAnySubstringMethod(invocation.TargetMethod))
                {
                    return true;
                }
            }

            return false;
        }

        private SyntaxNode ConvertOperandToArgument(in RequiredSymbols symbols, SyntaxGenerator generator, IOperation operand, Func<SyntaxNode, SyntaxNode> current)
        {
            var value = WalkDownBuiltInImplicitConversionOnConcatOperand(operand);

            //  Convert substring invocations to equivalent AsSpan invocation.
            if (value is IInvocationOperation invocation && symbols.IsAnySubstringMethod(invocation.TargetMethod))
            {
                SyntaxNode invocationSyntax = current(invocation.Syntax);

                //  Swap out parameter names if named-arguments are used. 
                if (TryGetNamedStartIndexArgument(symbols, invocation, out var namedStartIndexArgument))
                {
                    //  Both nodes are resolved against the invocation actually being rewritten, so that they stay
                    //  descendants of it whether or not the tracked node was found.
                    SyntaxNode argumentSyntax = invocationSyntax.GetCurrentNode(namedStartIndexArgument.Syntax) ?? namedStartIndexArgument.Syntax;
                    SyntaxNode startIndexSyntax = invocationSyntax.GetCurrentNode(namedStartIndexArgument.Value.Syntax) ?? namedStartIndexArgument.Value.Syntax;

                    var renamedArgumentSyntax = generator.Argument(AsSpanStartParameterName, RefKind.None, startIndexSyntax);
                    invocationSyntax = generator.ReplaceNode(invocationSyntax, argumentSyntax, renamedArgumentSyntax);
                }

                var asSpanInvocationSyntax = ReplaceInvocationMethodName(generator, invocationSyntax, AsSpanName).WithAddImportsAnnotation().WithAdditionalAnnotations(s_asSpanSymbolAnnotation);
                return generator.Argument(asSpanInvocationSyntax);
            }
            //  Character literals become string literals.
            else if (value.Type?.SpecialType == SpecialType.System_Char &&
                     value is ILiteralOperation literalOperation &&
                     literalOperation.ConstantValue.HasValue &&
                     literalOperation.ConstantValue.Value is { } literalValue)
            {
                var stringLiteral = generator.LiteralExpression(literalValue.ToString()).WithTriviaFrom(current(literalOperation.Syntax));
                return generator.Argument(stringLiteral);
            }
            else
            {
                return generator.Argument(current(value.Syntax));
            }
        }

        private bool TryGetNamedStartIndexArgument(in RequiredSymbols symbols, IInvocationOperation substringInvocation, [NotNullWhen(true)] out IArgumentOperation? namedStartIndexArgument)
        {
            RoslynDebug.Assert(symbols.IsAnySubstringMethod(substringInvocation.TargetMethod));

            foreach (var argument in substringInvocation.Arguments)
            {
                if (IsNamedArgument(argument) && symbols.IsAnySubstringStartIndexParameter(argument.Parameter))
                {
                    namedStartIndexArgument = argument;
                    return true;
                }
            }

            namedStartIndexArgument = default;
            return false;
        }
    }
}
