// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA2226: Operators should have symmetrical overloads
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class OperatorsShouldHaveSymmetricalOverloadsFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(OperatorsShouldHaveSymmetricalOverloadsAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    MicrosoftCodeQualityAnalyzersResources.Generate_missing_operators,
                    c => CreateChangedDocumentAsync(context, c),
                    nameof(MicrosoftCodeQualityAnalyzersResources.Generate_missing_operators)),
                context.Diagnostics);
            return Task.FromResult(true);
        }

        private static async Task<Document> CreateChangedDocumentAsync(
            CodeFixContext context, CancellationToken cancellationToken)
        {
            var document = context.Document;
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var semanticModel = editor.SemanticModel;
            var root = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
            var operatorNode = root.FindNode(context.Diagnostics.First().Location.SourceSpan);

            var containingOperator = (IMethodSymbol)semanticModel.GetDeclaredSymbol(operatorNode, cancellationToken);

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
            return editor.GetChangedDocument();
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