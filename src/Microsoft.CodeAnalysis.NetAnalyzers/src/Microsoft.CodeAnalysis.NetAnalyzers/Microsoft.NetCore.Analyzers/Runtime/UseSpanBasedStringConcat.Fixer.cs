// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;
using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;
using RequiredSymbols = Microsoft.NetCore.Analyzers.Runtime.UseSpanBasedStringConcat.RequiredSymbols;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    public abstract class UseSpanBasedStringConcatFixer : CodeFixProvider
    {
        private protected const string AsSpanName = nameof(MemoryExtensions.AsSpan);
        private protected const string AsSpanStartParameterName = "start";
        private protected const string ToStringName = nameof(ToString);

        private protected abstract SyntaxNode ReplaceInvocationMethodName(SyntaxGenerator generator, SyntaxNode invocationSyntax, string newName);

        private protected abstract bool IsSystemNamespaceImported(Project project, IReadOnlyList<SyntaxNode> namespaceImports);

        private protected abstract IOperation WalkDownBuiltInImplicitConversionOnConcatOperand(IOperation operand);

        private protected abstract bool IsNamedArgument(IArgumentOperation argumentOperation);

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(UseSpanBasedStringConcat.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var diagnostic = context.Diagnostics.First();
            var cancellationToken = context.CancellationToken;
            var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var compilation = model.Compilation;
            SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            if (!RequiredSymbols.TryGetSymbols(compilation, out var symbols))
                return;
            if (root.FindNode(context.Span, getInnermostNodeForTie: true) is not SyntaxNode concatExpressionSyntax)
                return;

            //  OperatorKind will be BinaryOperatorKind.Concatenate, even when '+' is used instead of '&' in Visual Basic.
            if (model.GetOperation(concatExpressionSyntax, cancellationToken) is not IBinaryOperation concatOperation
                || concatOperation.OperatorKind is not (BinaryOperatorKind.Add or BinaryOperatorKind.Concatenate))
            {
                return;
            }

            var operands = UseSpanBasedStringConcat.FlattenBinaryOperation(concatOperation);

            //  Bail out if we don't have a long enough span-based string.Concat overload.
            if (!symbols.TryGetRoscharConcatMethodWithArity(operands.Length, out IMethodSymbol? roscharConcatMethod))
                return;

            //  Bail if none of the operands are a non-conditional substring invocation. This could be the case if the
            //  only substring invocations in the expression were conditional invocations.
            if (!operands.Any(IsAnyNonConditionalSubstringInvocation))
                return;

            var codeAction = CodeAction.Create(
                Resx.UseSpanBasedStringConcatCodeFixTitle,
                FixConcatOperationChain,
                Resx.UseSpanBasedStringConcatCodeFixTitle);
            context.RegisterCodeFix(codeAction, diagnostic);

            return;

            // Local functions

            bool IsAnyNonConditionalSubstringInvocation(IOperation operation)
            {
                var value = WalkDownBuiltInImplicitConversionOnConcatOperand(operation);
                return value is IInvocationOperation invocation && symbols.IsAnySubstringMethod(invocation.TargetMethod);
            }

            async Task<Document> FixConcatOperationChain(CancellationToken cancellationToken)
            {
                RoslynDebug.Assert(roscharConcatMethod is not null);

                var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
                var generator = editor.Generator;

                SyntaxNode stringTypeNameSyntax = generator.TypeExpressionForStaticMemberAccess(symbols.StringType);
                SyntaxNode concatMemberAccessSyntax = generator.MemberAccessExpression(stringTypeNameSyntax, roscharConcatMethod.Name);

                //  Save leading and trailing trivia so it can be attached to the outside of the string.Concat invocation node.
                var leadingTrivia = operands.First().Syntax.GetLeadingTrivia();
                var trailingTrivia = operands.Last().Syntax.GetTrailingTrivia();

                var arguments = ImmutableArray.CreateBuilder<SyntaxNode>(operands.Length);
                foreach (var operand in operands)
                    arguments.Add(ConvertOperandToArgument(symbols, generator, operand));

                //  Strip off leading and trailing trivia from first and last operand nodes, respectively, and
                //  reattach it to the outside of the newly-created string.Concat invocation node.
                arguments[0] = arguments[0].WithoutLeadingTrivia();
                arguments[^1] = arguments[^1].WithoutTrailingTrivia();
                SyntaxNode concatMethodInvocationSyntax = generator.InvocationExpression(concatMemberAccessSyntax, arguments.MoveToImmutable())
                    .WithLeadingTrivia(leadingTrivia)
                    .WithTrailingTrivia(trailingTrivia);

                SyntaxNode newRoot = generator.ReplaceNode(root, concatExpressionSyntax, concatMethodInvocationSyntax);

                //  Import 'System' namespace if it's absent.
                if (!IsSystemNamespaceImported(context.Document.Project, generator.GetNamespaceImports(newRoot)))
                {
                    SyntaxNode systemNamespaceImport = generator.NamespaceImportDeclaration(nameof(System));
                    newRoot = generator.AddNamespaceImports(newRoot, systemNamespaceImport);
                }

                editor.ReplaceNode(root, newRoot);
                return editor.GetChangedDocument();
            }
        }

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        private SyntaxNode ConvertOperandToArgument(in RequiredSymbols symbols, SyntaxGenerator generator, IOperation operand)
        {
            var value = WalkDownBuiltInImplicitConversionOnConcatOperand(operand);

            //  Convert substring invocations to equivalent AsSpan invocation.
            if (value is IInvocationOperation invocation && symbols.IsAnySubstringMethod(invocation.TargetMethod))
            {
                SyntaxNode invocationSyntax = invocation.Syntax;

                //  Swap out parameter names if named-arguments are used. 
                if (TryGetNamedStartIndexArgument(symbols, invocation, out var namedStartIndexArgument))
                {
                    var renamedArgumentSyntax = generator.Argument(AsSpanStartParameterName, RefKind.None, namedStartIndexArgument.Value.Syntax);
                    invocationSyntax = generator.ReplaceNode(invocationSyntax, namedStartIndexArgument.Syntax, renamedArgumentSyntax);
                }
                var asSpanInvocationSyntax = ReplaceInvocationMethodName(generator, invocationSyntax, AsSpanName);
                return generator.Argument(asSpanInvocationSyntax);
            }
            //  Character literals become string literals.
            else if (value.Type.SpecialType == SpecialType.System_Char && value is ILiteralOperation literalOperation && literalOperation.ConstantValue.HasValue)
            {
                var stringLiteral = generator.LiteralExpression(literalOperation.ConstantValue.Value.ToString()).WithTriviaFrom(literalOperation.Syntax);
                return generator.Argument(stringLiteral);
            }
            else
            {
                return generator.Argument(value.Syntax);
            }

            bool TryGetNamedStartIndexArgument(in RequiredSymbols symbols, IInvocationOperation substringInvocation, [NotNullWhen(true)] out IArgumentOperation? namedStartIndexArgument)
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
}
