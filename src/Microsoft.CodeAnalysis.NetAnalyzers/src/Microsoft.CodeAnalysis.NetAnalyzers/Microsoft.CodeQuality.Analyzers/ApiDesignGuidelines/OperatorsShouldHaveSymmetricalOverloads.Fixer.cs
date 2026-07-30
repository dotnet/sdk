// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA2226: Operators should have symmetrical overloads
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class OperatorsShouldHaveSymmetricalOverloadsFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(OperatorsShouldHaveSymmetricalOverloadsAnalyzer.RuleId);

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            RegisterCodeFix(context, Generate_missing_operators, nameof(Generate_missing_operators));
            return Task.CompletedTask;
        }

        protected sealed override async Task ApplyFixAsync(
            Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var operatorNode = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);

            if (semanticModel.GetDeclaredSymbol(operatorNode, cancellationToken) is not IMethodSymbol containingOperator)
            {
                return;
            }

            Debug.Assert(containingOperator.IsUserDefinedOperator());

            var generator = editor.Generator;
            var newOperator = generator.OperatorDeclaration(
                GetInvertedOperatorKind(containingOperator),
                containingOperator.GetParameters().Select(p => generator.ParameterDeclaration(p)),
                generator.TypeExpression(containingOperator.ReturnType),
                containingOperator.DeclaredAccessibility,
                generator.GetModifiers(operatorNode),
                GetInvertedStatements(generator, containingOperator, semanticModel.Compilation));

            operatorNode = operatorNode.AncestorsAndSelf().First(a => a.RawKind == newOperator.RawKind);

            editor.InsertAfter(operatorNode, newOperator);
        }

        private static IEnumerable<SyntaxNode> GetInvertedStatements(
            SyntaxGenerator generator, IMethodSymbol containingOperator, Compilation compilation)
        {
            yield return GetInvertedStatement(generator, containingOperator, compilation);
        }

        private static SyntaxNode GetInvertedStatement(
            SyntaxGenerator generator, IMethodSymbol containingOperator, Compilation compilation)
        {
            if (containingOperator.Name == WellKnownMemberNames.EqualityOperatorName)
            {
                return generator.ReturnStatement(
                    generator.LogicalNotExpression(
                        generator.ValueEqualsExpression(
                            generator.IdentifierName(containingOperator.Parameters[0].Name),
                            generator.IdentifierName(containingOperator.Parameters[1].Name))));
            }
            else if (containingOperator.Name == WellKnownMemberNames.InequalityOperatorName)
            {
                return generator.ReturnStatement(
                    generator.LogicalNotExpression(
                        generator.ValueNotEqualsExpression(
                            generator.IdentifierName(containingOperator.Parameters[0].Name),
                            generator.IdentifierName(containingOperator.Parameters[1].Name))));
            }
            else
            {
                // If it's a  <   >   <=   or  >=   operator then we can't simply invert a call
                // to the existing operator.  i.e. the body of the "<" method should *not* be:
                //    return !(a > b);
                // Just provide a throwing impl for now.
                return generator.DefaultMethodStatement(compilation);
            }
        }

        private static OperatorKind GetInvertedOperatorKind(IMethodSymbol containingOperator)
        {
            return containingOperator.Name switch
            {
                WellKnownMemberNames.EqualityOperatorName => OperatorKind.Inequality,
                WellKnownMemberNames.InequalityOperatorName => OperatorKind.Equality,
                WellKnownMemberNames.LessThanOperatorName => OperatorKind.GreaterThan,
                WellKnownMemberNames.LessThanOrEqualOperatorName => OperatorKind.GreaterThanOrEqual,
                WellKnownMemberNames.GreaterThanOperatorName => OperatorKind.LessThan,
                WellKnownMemberNames.GreaterThanOrEqualOperatorName => OperatorKind.LessThanOrEqual,
                _ => throw new InvalidOperationException(),
            };
        }
    }
}