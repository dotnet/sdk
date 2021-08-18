// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
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
using RequiredSymbols = Microsoft.NetCore.Analyzers.Runtime.UseStringEqualsOverStringCompare.RequiredSymbols;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class UseStringEqualsOverStringCompareFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(UseStringEqualsOverStringCompare.RuleId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var token = context.CancellationToken;
            var semanticModel = await document.GetSemanticModelAsync(token).ConfigureAwait(false);

            _ = RequiredSymbols.TryGetSymbols(semanticModel.Compilation, out var symbols);
            RoslynDebug.Assert(symbols is not null);

            var root = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);
            var node = root.FindNode(context.Span, getInnermostNodeForTie: true);
            if (semanticModel.GetOperation(node, token) is not IBinaryOperation violation)
                return;

            //  Get the replacer that applies to the reported violation.
            var replacer = GetOperationReplacers(symbols).First(x => x.IsMatch(violation));

            var codeAction = CodeAction.Create(
                Resx.UseStringEqualsOverStringCompareCodeFixTitle,
                CreateChangedDocument,
                nameof(Resx.UseStringEqualsOverStringCompareCodeFixTitle));
            context.RegisterCodeFix(codeAction, context.Diagnostics);
            return;

            //  Local functions

            async Task<Document> CreateChangedDocument(CancellationToken token)
            {
                var editor = await DocumentEditor.CreateAsync(document, token).ConfigureAwait(false);
                var replacementNode = replacer.CreateReplacementExpression(violation, editor.Generator);
                editor.ReplaceNode(violation.Syntax, replacementNode);

                return editor.GetChangedDocument();
            }
        }

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        private static ImmutableArray<OperationReplacer> GetOperationReplacers(RequiredSymbols symbols)
        {
            return ImmutableArray.Create<OperationReplacer>(
                new StringStringCaseReplacer(symbols),
                new StringStringBoolReplacer(symbols),
                new StringStringStringComparisonReplacer(symbols));
        }

        /// <summary>
        /// Base class for an object that generate the replacement code for a reported violation.
        /// </summary>
        private abstract class OperationReplacer
        {
            protected OperationReplacer(RequiredSymbols symbols)
            {
                Symbols = symbols;
            }

            protected RequiredSymbols Symbols { get; }

            /// <summary>
            /// Indicates whether the current <see cref="OperationReplacer"/> applies to the specified violation.
            /// </summary>
            /// <param name="violation">The <see cref="IBinaryOperation"/> at the location reported by the analyzer.</param>
            /// <returns>True if the current <see cref="OperationReplacer"/> applies to the specified violation.</returns>
            public abstract bool IsMatch(IBinaryOperation violation);

            /// <summary>
            /// Creates a replacement node for a violation that the current <see cref="OperationReplacer"/> applies to.
            /// Asserts if the current <see cref="OperationReplacer"/> does not apply to the specified violation.
            /// </summary>
            /// <param name="violation">The <see cref="IBinaryOperation"/> obtained at the location reported by the analyzer.
            /// <see cref="IsMatch(IBinaryOperation)"/> must return <see langword="true"/> for this operation.</param>
            /// <param name="generator"></param>
            /// <returns></returns>
            public abstract SyntaxNode CreateReplacementExpression(IBinaryOperation violation, SyntaxGenerator generator);

            protected SyntaxNode CreateEqualsMemberAccess(SyntaxGenerator generator)
            {
                var stringTypeExpression = generator.TypeExpressionForStaticMemberAccess(Symbols.StringType);
                return generator.MemberAccessExpression(stringTypeExpression, nameof(string.Equals));
            }

            protected static IInvocationOperation GetInvocation(IBinaryOperation violation)
            {
                var result = UseStringEqualsOverStringCompare.GetInvocationFromEqualityCheckWithLiteralZero(violation);

                RoslynDebug.Assert(result is not null);

                return result;
            }

            protected static SyntaxNode InvertIfNotEquals(SyntaxNode stringEqualsInvocationExpression, IBinaryOperation equalsOrNotEqualsOperation, SyntaxGenerator generator)
            {
                return equalsOrNotEqualsOperation.OperatorKind is BinaryOperatorKind.NotEquals ?
                    generator.LogicalNotExpression(stringEqualsInvocationExpression) :
                    stringEqualsInvocationExpression;
            }
        }

        /// <summary>
        /// Replaces <see cref="string.Compare(string, string)"/> violations.
        /// </summary>
        private sealed class StringStringCaseReplacer : OperationReplacer
        {
            public StringStringCaseReplacer(RequiredSymbols symbols)
                : base(symbols)
            { }

            public override bool IsMatch(IBinaryOperation violation) => UseStringEqualsOverStringCompare.IsStringStringCase(violation, Symbols);

            public override SyntaxNode CreateReplacementExpression(IBinaryOperation violation, SyntaxGenerator generator)
            {
                RoslynDebug.Assert(IsMatch(violation));

                var compareInvocation = GetInvocation(violation);
                var equalsInvocationSyntax = generator.InvocationExpression(
                    CreateEqualsMemberAccess(generator),
                    compareInvocation.Arguments.GetArgumentsInParameterOrder().Select(x => x.Value.Syntax));

                return InvertIfNotEquals(equalsInvocationSyntax, violation, generator);
            }
        }

        /// <summary>
        /// Replaces <see cref="string.Compare(string, string, bool)"/> violations.
        /// </summary>
        private sealed class StringStringBoolReplacer : OperationReplacer
        {
            public StringStringBoolReplacer(RequiredSymbols symbols)
                : base(symbols)
            { }

            public override bool IsMatch(IBinaryOperation violation) => UseStringEqualsOverStringCompare.IsStringStringBoolCase(violation, Symbols);

            public override SyntaxNode CreateReplacementExpression(IBinaryOperation violation, SyntaxGenerator generator)
            {
                RoslynDebug.Assert(IsMatch(violation));

                var compareInvocation = GetInvocation(violation);

                //  We know that the 'ignoreCase' argument in 'string.Compare(string, string, bool)' is a boolean literal
                //  because we've asserted that 'IsMatch' returns true.
                var ignoreCaseLiteral = (ILiteralOperation)compareInvocation.Arguments.GetArgumentForParameterAtIndex(2).Value;

                //  If the violation contains a call to 'string.Compare(x, y, true)' then we
                //  replace it with a call to 'string.Equals(x, y, StringComparison.CurrentCultureIgnoreCase)'.
                //  If the violation contains a call to 'string.Compare(x, y, false)' then we
                //  replace it with a call to 'string.Equals(x, y, StringComparison.CurrentCulture)'. 
                var stringComparisonEnumMemberName = (bool)ignoreCaseLiteral.ConstantValue.Value ?
                    nameof(StringComparison.CurrentCultureIgnoreCase) :
                    nameof(StringComparison.CurrentCulture);
                var stringComparisonMemberAccessSyntax = generator.MemberAccessExpression(
                    generator.TypeExpressionForStaticMemberAccess(Symbols.StringComparisonType),
                    stringComparisonEnumMemberName);

                var equalsInvocationSyntax = generator.InvocationExpression(
                    CreateEqualsMemberAccess(generator),
                    compareInvocation.Arguments.GetArgumentForParameterAtIndex(0).Value.Syntax,
                    compareInvocation.Arguments.GetArgumentForParameterAtIndex(1).Value.Syntax,
                    stringComparisonMemberAccessSyntax);

                return InvertIfNotEquals(equalsInvocationSyntax, violation, generator);
            }
        }

        /// <summary>
        /// Replaces <see cref="string.Compare(string, string, StringComparison)"/> violations.
        /// </summary>
        private sealed class StringStringStringComparisonReplacer : OperationReplacer
        {
            public StringStringStringComparisonReplacer(RequiredSymbols symbols)
                : base(symbols)
            { }

            public override bool IsMatch(IBinaryOperation violation) => UseStringEqualsOverStringCompare.IsStringStringStringComparisonCase(violation, Symbols);

            public override SyntaxNode CreateReplacementExpression(IBinaryOperation violation, SyntaxGenerator generator)
            {
                RoslynDebug.Assert(IsMatch(violation));

                var invocation = GetInvocation(violation);
                var equalsInvocationSyntax = generator.InvocationExpression(
                    CreateEqualsMemberAccess(generator),
                    invocation.Arguments.GetArgumentsInParameterOrder().Select(x => x.Value.Syntax));

                return InvertIfNotEquals(equalsInvocationSyntax, violation, generator);
            }
        }
    }
}
