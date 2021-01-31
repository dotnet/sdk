// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
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
        private const string AsSpanName = nameof(MemoryExtensions.AsSpan);
        private const string AsSpanStartParameterName = "start";
        private protected const string ToStringName = nameof(ToString);

        private protected abstract SyntaxNode ReplaceInvocationMethodName(SyntaxGenerator generator, SyntaxNode invocationSyntax, string newName);

        private protected abstract SyntaxToken GetOperatorToken(IBinaryOperation binaryOperation);

        private protected abstract bool IsSystemNamespaceImported(IReadOnlyList<SyntaxNode> namespaceImports);

        private protected abstract bool IsNamedArgument(IArgumentOperation argument);

        /// <summary>
        /// Invokes <see cref="object.ToString"/> on the specified expression using the Elvis operator.
        /// </summary>
        private protected abstract SyntaxNode CreateConditionalToStringInvocation(SyntaxNode receiverExpression);

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(UseSpanBasedStringConcat.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var diagnostic = context.Diagnostics.First();
            var cancellationToken = context.CancellationToken;
            var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var compilation = model.Compilation;
            SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            //  Bail out early if we're missing anything we need.
            if (!RequiredSymbols.TryGetSymbols(compilation, out var symbols))
                return;
            if (root.FindNode(context.Span, getInnermostNodeForTie: true) is not SyntaxNode concatExpressionSyntax)
                return;
            if (model.GetOperation(concatExpressionSyntax, cancellationToken) is not IBinaryOperation concatOperation || concatOperation.OperatorKind != symbols.ConcatOperatorKind)
                return;

            var operands = UseSpanBasedStringConcat.FlattenBinaryOperationChain(concatOperation);
            //  Bail out if we don't have a long enough span-based string.Concat overload.
            if (!symbols.TryGetRoscharConcatMethodWithArity(operands.Length, out IMethodSymbol? roscharConcatMethod))
                return;

            var codeAction = CodeAction.Create(
                Resx.UseSpanBasedStringConcatTitle,
                FixConcatOperationChain,
                Resx.UseSpanBasedStringConcatTitle);
            context.RegisterCodeFix(codeAction, diagnostic);

            async Task<Document> FixConcatOperationChain(CancellationToken cancellationToken)
            {
                RoslynDebug.Assert(roscharConcatMethod is not null);

                var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
                var generator = editor.Generator;

                //  Save leading and trailing trivia so it can be attached to the outside of the 'string.Concat(...)' invocation expression.
                var leadingTrivia = operands.First().Syntax.GetLeadingTrivia();
                var trailingTrivia = operands.Last().Syntax.GetTrailingTrivia();

                SyntaxNode stringTypeNameSyntax = generator.TypeExpressionForStaticMemberAccess(symbols.StringType);
                SyntaxNode concatMemberAccessSyntax = generator.MemberAccessExpression(stringTypeNameSyntax, roscharConcatMethod.Name);
                var arguments = GenerateConcatMethodArguments(symbols, generator, operands);
                SyntaxNode concatMethodInvocationSyntax = generator.InvocationExpression(concatMemberAccessSyntax, arguments)
                    .WithLeadingTrivia(leadingTrivia)
                    .WithTrailingTrivia(trailingTrivia);
                var newRoot = generator.ReplaceNode(root, concatExpressionSyntax, concatMethodInvocationSyntax);

                //  Make sure 'System' namespace is imported.
                if (!IsSystemNamespaceImported(generator.GetNamespaceImports(newRoot)))
                {
                    var systemNamespaceImport = generator.NamespaceImportDeclaration(nameof(System));
                    newRoot = generator.AddNamespaceImports(newRoot, systemNamespaceImport);
                }

                editor.ReplaceNode(root, newRoot);
                return editor.GetChangedDocument();
            }
        }

        private ImmutableArray<SyntaxNode> GenerateConcatMethodArguments(in RequiredSymbols symbols, SyntaxGenerator generator, ImmutableArray<IOperation> operands)
        {
            var builder = ImmutableArray.CreateBuilder<SyntaxNode>(operands.Length);
            foreach (IOperation operand in operands)
            {
                //  Convert 'Substring' invocations into 'AsSpan' invocations.
                if (WalkDownImplicitConversion(operand) is IInvocationOperation invocation && symbols.IsAnySubstringMethod(invocation.TargetMethod))
                {
                    SyntaxNode newInvocationSyntax = invocation.Syntax;

                    //  Convert 'Substring' named-arguments to equivalent 'AsSpan' named arguments.
                    IArgumentOperation? namedStartIndexArgument = GetNamedStartIndexArgumentOrDefault(symbols, invocation);
                    if (namedStartIndexArgument is not null)
                    {
                        SyntaxNode renamedSubstringArgumentSyntax = generator.Argument(AsSpanStartParameterName, RefKind.None, namedStartIndexArgument.Value.Syntax);
                        newInvocationSyntax = generator.ReplaceNode(newInvocationSyntax, namedStartIndexArgument.Syntax, renamedSubstringArgumentSyntax);
                    }

                    //  Replace 'Substring' identifier with 'AsSpan', leaving the rest of the node (including trivia) intact. 
                    newInvocationSyntax = ReplaceInvocationMethodName(generator, newInvocationSyntax, AsSpanName);
                    builder.Add(generator.Argument(newInvocationSyntax));
                }
                else
                {
                    IOperation value = WalkDownImplicitConversion(operand);
                    if (value.Type.SpecialType == SpecialType.System_String)
                    {
                        builder.Add(generator.Argument(value.Syntax));
                    }
                    else
                    {
                        SyntaxNode newValueSyntax;
                        if (value.Type.IsReferenceTypeOrNullableValueType())
                        {
                            newValueSyntax = CreateConditionalToStringInvocation(value.Syntax);
                        }
                        else
                        {
                            SyntaxNode toStringMemberAccessSyntax = generator.MemberAccessExpression(value.Syntax.WithoutTrivia(), ToStringName);
                            newValueSyntax = generator.InvocationExpression(toStringMemberAccessSyntax, Array.Empty<SyntaxNode>())
                                .WithTriviaFrom(value.Syntax);
                        }
                        builder.Add(generator.Argument(newValueSyntax));
                    }
                }
            }

            builder[0] = builder[0].WithoutLeadingTrivia();
            builder[^1] = builder[^1].WithoutTrailingTrivia();
            return builder.MoveToImmutable();

            //  If the 'startIndex' argument was passed using named-arguments, return it. 
            //  Otherwise, return null.
            IArgumentOperation? GetNamedStartIndexArgumentOrDefault(in RequiredSymbols symbols, IInvocationOperation substringInvocation)
            {
                RoslynDebug.Assert(symbols.IsAnySubstringMethod(substringInvocation.TargetMethod));
                foreach (var argument in substringInvocation.Arguments)
                {
                    if (IsNamedArgument(argument) && symbols.IsAnySubstringStartIndexParameter(argument.Parameter))
                        return argument;
                }
                return null;
            }

            static IOperation WalkDownImplicitConversion(IOperation operand)
            {
                if (operand is IConversionOperation { Type: { SpecialType: SpecialType.System_Object or SpecialType.System_String }, IsImplicit: true } conversion)
                    return conversion.Operand;
                return operand;
            }
        }

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
    }
}
