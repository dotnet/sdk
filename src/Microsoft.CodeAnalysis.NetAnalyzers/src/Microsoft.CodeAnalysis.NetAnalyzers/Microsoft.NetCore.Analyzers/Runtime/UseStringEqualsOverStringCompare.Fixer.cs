// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;
using RequiredSymbols = Microsoft.NetCore.Analyzers.Runtime.UseStringEqualsOverStringCompare.RequiredSymbols;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class UseStringEqualsOverStringCompareFixer : CodeFixProvider
    {
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var token = context.CancellationToken;
            var semanticModel = await document.GetSemanticModelAsync(token).ConfigureAwait(false);

            if (!RequiredSymbols.TryGetSymbols(semanticModel.Compilation, out var symbols))
                return;

            var root = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);

            if (semanticModel.GetOperation(root.FindNode(context.Span, getInnermostNodeForTie: true), token) is not IBinaryOperation operation)
                return;

            var selectors = GetSelectors(symbols);

            foreach (var selector in selectors)
            {
                if (selector.IsMatch(operation))
                {
                    var codeAction = CodeAction.Create(
                        Resx.UseStringEqualsOverStringCompareCodeFixTitle,
                        async token =>
                        {
                            var editor = await DocumentEditor.CreateAsync(document, token).ConfigureAwait(false);
                            var replacementNode = selector.GetReplacementExpression(operation, editor.Generator);
                            editor.ReplaceNode(operation.Syntax, replacementNode);
                            return editor.GetChangedDocument();
                        }, Resx.UseStringEqualsOverStringCompareCodeFixTitle);
                    context.RegisterCodeFix(codeAction, context.Diagnostics);
                    break;
                }
            }
        }

        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(UseStringEqualsOverStringCompare.RuleId);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        private static ImmutableArray<OperationSelector> GetSelectors(RequiredSymbols symbols)
        {
            return ImmutableArray.Create<OperationSelector>(
                new StringStringSelector(symbols),
                new StringStringBoolSelector(symbols),
                new StringStringStringComparisonSelector(symbols));
        }

        /// <summary>
        /// Gets a function that selects an operation that should have a diagnostic reported for it.
        /// We have to use this round-about way of implementing the analyzer because <see cref="OperationSelector"/> has a method
        /// that takes a <see cref="SyntaxGenerator"/>, and analyzers aren't allowed to reference that type in any way. Having the
        /// analyzer use the <see cref="OperationSelector"/>s indirectly via this method avoids having duplicated logic in the analyzer and fixer.
        /// </summary>
        /// <param name="symbols"></param>
        /// <returns></returns>
        internal static Func<IOperation, bool> GetOperationSelector(RequiredSymbols symbols)
        {
            var selectors = GetSelectors(symbols);

            return operation =>
            {
                foreach (var selector in selectors)
                {
                    if (selector.IsMatch(operation))
                        return true;
                }

                return false;
            };
        }

        /// <summary>
        /// Selects an equals or not-equals operation where one argument is a literal zero, and the other argument
        /// is an eligible <c>string.Compare</c> invocation.
        /// </summary>
        private abstract class OperationSelector
        {
            protected OperationSelector(RequiredSymbols symbols)
            {
                Symbols = symbols;
            }

            protected RequiredSymbols Symbols { get; }

            /// <summary>
            /// Indicates whether the specified <see cref="IOperation"/> matches the current <see cref="OperationSelector"/>
            /// </summary>
            /// <param name="compareResultToLiteralZero"></param>
            /// <returns></returns>
            public abstract bool IsMatch(IOperation compareResultToLiteralZero);

            /// <summary>
            /// Creates a replacement expression for the specified matching <see cref="IOperation"/>. Asserts if
            /// <see cref="IsMatch(IOperation)"/> returns <see langword="false" /> for the specified <see cref="IOperation"/>.
            /// </summary>
            /// <param name="compareResultToLiteralZero">An <see cref="IOperation"/> that is matched by the current <see cref="OperationSelector"/></param>
            /// <param name="generator">The <see cref="SyntaxGenerator"/> to use.</param>
            /// <returns>The replacement expression to be used by the code fixer.</returns>
            public abstract SyntaxNode GetReplacementExpression(IOperation compareResultToLiteralZero, SyntaxGenerator generator);

            /// <summary>
            /// Tries to get an invocation operation that is being compared to a literal zero.
            /// </summary>
            /// <param name="compareResultToLiteralZero">An operation that is potentially a comparison of an invocation with a literal zero.</param>
            /// <param name="invocation">The invocation operation.</param>
            /// <returns>True if the specified operation is an equals or not-equals operation that compares a literal zero to 
            /// any invocation operation.</returns>
            protected static bool TryGetInvocationFromComparisonWithLiteralZero(IOperation compareResultToLiteralZero, [NotNullWhen(true)] out IInvocationOperation? invocation)
            {
                if (compareResultToLiteralZero is IBinaryOperation binaryOperation &&
                    binaryOperation.OperatorKind is BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals)
                {
                    if (TryConvertOperands(binaryOperation.LeftOperand, binaryOperation.RightOperand, out invocation) ||
                        TryConvertOperands(binaryOperation.RightOperand, binaryOperation.LeftOperand, out invocation))
                    {
                        return true;
                    }
                }

                invocation = default;
                return false;

                //  Local functions

                static bool TryConvertOperands(IOperation first, IOperation second, [NotNullWhen(true)] out IInvocationOperation? result)
                {
                    if (first is IInvocationOperation invocation &&
                        second is ILiteralOperation literal &&
                        literal.ConstantValue.HasValue &&
                        literal.ConstantValue.Value is int integer &&
                        integer is 0)
                    {
                        result = invocation;
                        return true;
                    }

                    result = default;
                    return false;
                }
            }

            protected static IInvocationOperation GetInvocationFromComparisonWithLiteralZero(IOperation compareResultToLiteralZero)
            {
                if (!TryGetInvocationFromComparisonWithLiteralZero(compareResultToLiteralZero, out var invocation))
                    Fail();

                return invocation;
            }

            protected static bool IsNotEqualsOperation(IOperation operation)
            {
                return operation is IBinaryOperation binaryOperation &&
                    binaryOperation.OperatorKind is BinaryOperatorKind.NotEquals;
            }

            [DoesNotReturn]
#pragma warning disable CS8763 // A method marked [DoesNotReturn] should not return.
            protected static void Fail() => Debug.Fail($"'{nameof(GetReplacementExpression)}' must only be called when '{nameof(IsMatch)}' is 'true'.");
#pragma warning restore CS8763 // A method marked [DoesNotReturn] should not return.

            protected SyntaxNode CreateStringEqualsMemberAccessExpression(SyntaxGenerator generator)
            {
                var stringTypeExpression = generator.TypeExpressionForStaticMemberAccess(Symbols.StringType);
                return generator.MemberAccessExpression(stringTypeExpression, nameof(string.Equals));
            }
        }

        /// <summary>
        /// Selects <see cref="IOperation"/>s that satisfy all of the following:
        /// <list type="bullet">
        /// <item>Is an equals or not-equals operation</item>
        /// <item>One operand is a literal zero</item>
        /// <item>The other operand is an invocation of <see cref="string.Compare(string, string)"/></item>
        /// </list>
        /// </summary>
        private sealed class StringStringSelector : OperationSelector
        {
            public StringStringSelector(RequiredSymbols symbols)
                : base(symbols)
            { }

            public override bool IsMatch(IOperation compareResultToLiteralZero)
            {
                return TryGetInvocationFromComparisonWithLiteralZero(compareResultToLiteralZero, out var invocation) &&
                    invocation.TargetMethod.Equals(Symbols.CompareStringString, SymbolEqualityComparer.Default);
            }

            public override SyntaxNode GetReplacementExpression(IOperation compareResultToLiteralZero, SyntaxGenerator generator)
            {
                RoslynDebug.Assert(IsMatch(compareResultToLiteralZero));

                var invocation = GetInvocationFromComparisonWithLiteralZero(compareResultToLiteralZero);
                var equalsMemberAccessExpression = CreateStringEqualsMemberAccessExpression(generator);
                var equalsInvocationExpression = generator.InvocationExpression(
                    equalsMemberAccessExpression,
                    invocation.Arguments.GetArgumentsInParameterOrder().Select(x => x.Value.Syntax));

                return IsNotEqualsOperation(compareResultToLiteralZero) ?
                    generator.LogicalNotExpression(equalsInvocationExpression) :
                    equalsInvocationExpression;
            }
        }

        /// <summary>
        /// Selects <see cref="IOperation"/>s that satisfy all of the following:
        /// <list type="bullet">
        /// <item>Is an equals or not-equals operation</item>
        /// <item>One operand is a literal zero</item>
        /// <item>The other operand is an invocation of <see cref="string.Compare(string, string, bool)"/></item>
        /// <item>The <see langword="bool"/> argument is a literal</item>
        /// </list>
        /// </summary>
        private sealed class StringStringBoolSelector : OperationSelector
        {
            public StringStringBoolSelector(RequiredSymbols symbols)
                : base(symbols)
            { }

            public override bool IsMatch(IOperation compareResultToLiteralZero)
            {
                //  The 'ignoreCase' bool argument of string.Compare must be a literal.
                return TryGetInvocationFromComparisonWithLiteralZero(compareResultToLiteralZero, out var invocation) &&
                    invocation.TargetMethod.Equals(Symbols.CompareStringStringBool, SymbolEqualityComparer.Default) &&
                    invocation.Arguments.GetArgumentForParameterAtIndex(2).Value is ILiteralOperation boolLiteral &&
                    boolLiteral.ConstantValue.HasValue &&
                    boolLiteral.ConstantValue.Value is bool;
            }

            public override SyntaxNode GetReplacementExpression(IOperation compareResultToLiteralZero, SyntaxGenerator generator)
            {
                RoslynDebug.Assert(IsMatch(compareResultToLiteralZero));

                var invocation = GetInvocationFromComparisonWithLiteralZero(compareResultToLiteralZero);

                //  'IsMatch' rejects operations where the 'ignoreCase' argument is not a literal.
                var ignoreCaseLiteral = (ILiteralOperation)invocation.Arguments.GetArgumentForParameterAtIndex(2).Value;

                var equalsMemberAccessExpression = CreateStringEqualsMemberAccessExpression(generator);
                var stringComparisonTypeExpression = generator.TypeExpressionForStaticMemberAccess(Symbols.StringComparisonType);

                //  Convert 'ignoreCase' boolean argument to equivalent StringComparison value.
                var stringComparisonEnumMemberName = (bool)ignoreCaseLiteral.ConstantValue.Value ?
                    nameof(StringComparison.CurrentCultureIgnoreCase) :
                    nameof(StringComparison.CurrentCulture);
                var stringComparisonEnumMemberAccessExpression = generator.MemberAccessExpression(stringComparisonTypeExpression, stringComparisonEnumMemberName);

                var equalsInvocationExpression = generator.InvocationExpression(
                    equalsMemberAccessExpression,
                    invocation.Arguments.GetArgumentForParameterAtIndex(0).Value.Syntax,
                    invocation.Arguments.GetArgumentForParameterAtIndex(1).Value.Syntax,
                    stringComparisonEnumMemberAccessExpression);

                return IsNotEqualsOperation(compareResultToLiteralZero) ?
                    generator.LogicalNotExpression(equalsInvocationExpression) :
                    equalsInvocationExpression;
            }
        }

        /// <summary>
        /// Selects <see cref="IOperation"/>s that satisfy all of the following:
        /// <list type="bullet">
        /// <item>Is an equals or not-equals operation</item>
        /// <item>One operand is a literal zero</item>
        /// <item>The other operand is an invocation of <see cref="string.Compare(string, string, StringComparison)"/></item>
        /// </list>
        /// </summary>
        private sealed class StringStringStringComparisonSelector : OperationSelector
        {
            public StringStringStringComparisonSelector(RequiredSymbols symbols)
                : base(symbols)
            { }

            public override bool IsMatch(IOperation compareResultToLiteralZero)
            {
                return TryGetInvocationFromComparisonWithLiteralZero(compareResultToLiteralZero, out var invocation) &&
                    invocation.TargetMethod.Equals(Symbols.CompareStringStringStringComparison, SymbolEqualityComparer.Default);
            }

            public override SyntaxNode GetReplacementExpression(IOperation compareResultToLiteralZero, SyntaxGenerator generator)
            {
                RoslynDebug.Assert(IsMatch(compareResultToLiteralZero));

                var invocation = GetInvocationFromComparisonWithLiteralZero(compareResultToLiteralZero);

                var equalsMemberAccessExpression = CreateStringEqualsMemberAccessExpression(generator);

                var equalsInvocationExpression = generator.InvocationExpression(
                    equalsMemberAccessExpression,
                    invocation.Arguments.GetArgumentsInParameterOrder().Select(x => x.Value.Syntax));

                return IsNotEqualsOperation(compareResultToLiteralZero) ?
                    generator.LogicalNotExpression(equalsInvocationExpression) :
                    equalsInvocationExpression;
            }
        }
    }
}
