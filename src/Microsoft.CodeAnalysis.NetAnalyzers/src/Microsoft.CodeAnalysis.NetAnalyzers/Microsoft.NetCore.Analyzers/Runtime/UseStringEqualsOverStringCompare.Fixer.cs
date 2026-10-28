// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;
using RequiredSymbols = Microsoft.NetCore.Analyzers.Runtime.UseStringEqualsOverStringCompare.RequiredSymbols;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class UseStringEqualsOverStringCompareFixer : SyntaxEditorBasedCodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(UseStringEqualsOverStringCompare.RuleId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (GetViolation(root, context.Span, semanticModel, context.CancellationToken) is null)
            {
                return;
            }

            RegisterCodeFix(context, Resx.UseStringEqualsOverStringCompareCodeFixTitle, nameof(Resx.UseStringEqualsOverStringCompareCodeFixTitle));
        }

        protected override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (GetViolation(editor.OriginalRoot, diagnostic.Location.SourceSpan, semanticModel, cancellationToken) is not (IOperation violation, OperationReplacer replacer))
            {
                return;
            }

            //  The replacement is built out of the reported node's own descendants, so track them: a nested
            //  violation may already have been rewritten by the time this fix runs.
            foreach (var argument in replacer.GetArgumentSyntaxes(violation))
            {
                editor.TrackNode(argument);
            }

            editor.ReplaceNode(violation.Syntax, (currentNode, generator) =>
                replacer.CreateReplacementExpression(violation, generator, original => currentNode.GetCurrentNode(original) ?? original));
        }

        private static (IOperation Violation, OperationReplacer Replacer)? GetViolation(SyntaxNode root, TextSpan span, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            if (!RequiredSymbols.TryGetSymbols(semanticModel.Compilation, out var symbols))
            {
                return null;
            }

            var node = root.FindNode(span, getInnermostNodeForTie: true);
            var violation = semanticModel.GetOperation(node, cancellationToken);
            if (violation is not (IBinaryOperation or IInvocationOperation))
            {
                return null;
            }

            var replacer = GetOperationReplacers(symbols).FirstOrDefault(x => x.IsMatch(violation));

            return replacer is not null ? (violation, replacer) : null;
        }

        private static ImmutableArray<OperationReplacer> GetOperationReplacers(RequiredSymbols symbols)
        {
            return ImmutableArray.Create<OperationReplacer>(
                new StringStringCaseReplacer(symbols),
                new StringStringBoolReplacer(symbols),
                new StringStringStringComparisonReplacer(symbols),
                new OrdinalStringStringCaseReplacer(symbols));
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
            /// <param name="violation">The <see cref="IBinaryOperation"/> or <see cref="IInvocationOperation"/> at the location reported by the analyzer.</param>
            /// <returns>True if the current <see cref="OperationReplacer"/> applies to the specified violation.</returns>
            public abstract bool IsMatch(IOperation violation);

            /// <summary>
            /// Creates a replacement node for a violation that the current <see cref="OperationReplacer"/> applies to.
            /// Asserts if the current <see cref="OperationReplacer"/> does not apply to the specified violation.
            /// </summary>
            /// <param name="violation">The <see cref="IBinaryOperation"/> or <see cref="IInvocationOperation"/> obtained at the location reported by the analyzer.
            /// <see cref="IsMatch(IOperation)"/> must return <see langword="true"/> for this operation.</param>
            /// <param name="generator"></param>
            /// <param name="current">Maps a descendant of the violation onto its current form in the tree being edited.</param>
            /// <returns></returns>
            public abstract SyntaxNode CreateReplacementExpression(IOperation violation, SyntaxGenerator generator, Func<SyntaxNode, SyntaxNode> current);

            /// <summary>
            /// Gets the syntax nodes that <see cref="CreateReplacementExpression"/> carries over from the violation.
            /// </summary>
            public IEnumerable<SyntaxNode> GetArgumentSyntaxes(IOperation violation)
                => GetInvocation(violation).Arguments.Select(x => x.Value.Syntax);

            protected SyntaxNode CreateEqualsMemberAccess(SyntaxGenerator generator)
            {
                var stringTypeExpression = generator.TypeExpressionForStaticMemberAccess(Symbols.StringType);
                return generator.MemberAccessExpression(stringTypeExpression, nameof(string.Equals));
            }

            protected IInvocationOperation GetInvocation(IOperation violation)
            {
                var result = violation switch
                {
                    IBinaryOperation b => UseStringEqualsOverStringCompare.GetInvocationFromEqualityCheckWithLiteralZero(b),
                    IInvocationOperation i => UseStringEqualsOverStringCompare.GetInvocationFromEqualsCheckWithLiteralZero(i, Symbols.IntEquals),
                    _ => throw new NotSupportedException()
                };

                RoslynDebug.Assert(result is not null);

                return result;
            }

            protected static SyntaxNode InvertIfNotEquals(SyntaxNode stringEqualsInvocationExpression, IOperation equalsOrNotEqualsOperation, SyntaxGenerator generator)
            {
                if (equalsOrNotEqualsOperation is IBinaryOperation b)
                {
                    return b.OperatorKind is BinaryOperatorKind.NotEquals
                        ? generator.LogicalNotExpression(stringEqualsInvocationExpression)
                        : stringEqualsInvocationExpression;
                }

                if (equalsOrNotEqualsOperation is IInvocationOperation i)
                {
                    return i.Instance?.Parent is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not }
                        ? generator.LogicalNotExpression(stringEqualsInvocationExpression)
                        : stringEqualsInvocationExpression;
                }

                throw new NotSupportedException();
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

            public override bool IsMatch(IOperation violation) => UseStringEqualsOverStringCompare.IsStringStringCase(violation, Symbols);

            public override SyntaxNode CreateReplacementExpression(IOperation violation, SyntaxGenerator generator, Func<SyntaxNode, SyntaxNode> current)
            {
                RoslynDebug.Assert(IsMatch(violation));

                var compareInvocation = GetInvocation(violation);
                var equalsInvocationSyntax = generator.InvocationExpression(
                    CreateEqualsMemberAccess(generator),
                    compareInvocation.Arguments.GetArgumentsInParameterOrder().Select(x => current(x.Value.Syntax)));

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

            public override bool IsMatch(IOperation violation) => UseStringEqualsOverStringCompare.IsStringStringBoolCase(violation, Symbols);

            public override SyntaxNode CreateReplacementExpression(IOperation violation, SyntaxGenerator generator, Func<SyntaxNode, SyntaxNode> current)
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
                var stringComparisonEnumMemberName = ignoreCaseLiteral.ConstantValue.Value is true ?
                    nameof(StringComparison.CurrentCultureIgnoreCase) :
                    nameof(StringComparison.CurrentCulture);
                var stringComparisonMemberAccessSyntax = generator.MemberAccessExpression(
                    generator.TypeExpressionForStaticMemberAccess(Symbols.StringComparisonType),
                    stringComparisonEnumMemberName);

                var equalsInvocationSyntax = generator.InvocationExpression(
                    CreateEqualsMemberAccess(generator),
                    current(compareInvocation.Arguments.GetArgumentForParameterAtIndex(0).Value.Syntax),
                    current(compareInvocation.Arguments.GetArgumentForParameterAtIndex(1).Value.Syntax),
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

            public override bool IsMatch(IOperation violation) => UseStringEqualsOverStringCompare.IsStringStringStringComparisonCase(violation, Symbols);

            public override SyntaxNode CreateReplacementExpression(IOperation violation, SyntaxGenerator generator, Func<SyntaxNode, SyntaxNode> current)
            {
                RoslynDebug.Assert(IsMatch(violation));

                var invocation = GetInvocation(violation);
                var equalsInvocationSyntax = generator.InvocationExpression(
                    CreateEqualsMemberAccess(generator),
                    invocation.Arguments.GetArgumentsInParameterOrder().Select(x => current(x.Value.Syntax)));

                return InvertIfNotEquals(equalsInvocationSyntax, violation, generator);
            }
        }

        /// <summary>
        /// Replaces <see cref="string.CompareOrdinal(string, string)"/> violations.
        /// </summary>
        private sealed class OrdinalStringStringCaseReplacer : OperationReplacer
        {
            public OrdinalStringStringCaseReplacer(RequiredSymbols symbols)
                : base(symbols)
            { }

            public override bool IsMatch(IOperation violation) => UseStringEqualsOverStringCompare.IsOrdinalStringStringCase(violation, Symbols);

            public override SyntaxNode CreateReplacementExpression(IOperation violation, SyntaxGenerator generator, Func<SyntaxNode, SyntaxNode> current)
            {
                RoslynDebug.Assert(IsMatch(violation));

                var compareInvocation = GetInvocation(violation);
                var equalsInvocationSyntax = generator.InvocationExpression(
                    CreateEqualsMemberAccess(generator),
                    compareInvocation.Arguments.GetArgumentsInParameterOrder().Select(x => current(x.Value.Syntax)));

                return InvertIfNotEquals(equalsInvocationSyntax, violation, generator);
            }
        }
    }
}
