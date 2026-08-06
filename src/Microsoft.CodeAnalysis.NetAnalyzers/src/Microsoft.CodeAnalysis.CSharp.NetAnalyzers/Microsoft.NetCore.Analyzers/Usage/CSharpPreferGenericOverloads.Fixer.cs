// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.NetCore.Analyzers.Usage;
using static Microsoft.NetCore.Analyzers.Usage.PreferGenericOverloadsAnalyzer;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
    /// <summary>
    /// CA2263: <inheritdoc cref="NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.PreferGenericOverloadsTitle"/>
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpPreferGenericOverloadsFixer : PreferGenericOverloadsFixer
    {
        protected override async Task<Document> ReplaceWithGenericCallAsync(Document document, IInvocationOperation invocation, CancellationToken cancellationToken)
        {
            if (!RuntimeTypeInvocationContext.TryGetContext(invocation, out var invocationContext))
            {
                return document;
            }

            var modifiedInvocationSyntax = CSharpPreferGenericOverloadsAnalyzer.GetModifiedInvocationSyntax(invocationContext);

            if (modifiedInvocationSyntax is not InvocationExpressionSyntax invocationExpressionSyntax)
            {
                return document;
            }

            // Analyzers are not allowed to have a reference to Simplifier, so add the additional annotation here instead.
            invocationExpressionSyntax = invocationExpressionSyntax.WithExpression(invocationExpressionSyntax.Expression.WithAdditionalAnnotations(Simplifier.Annotation));

            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            if (invocationContext.Parent is IConversionOperation conversionOperation
                && invocationContext.Parent.Syntax is CastExpressionSyntax castExpressionSyntax
                && invocationContext.SemanticModel is not null)
            {
                var typeInfo = invocationContext.SemanticModel.GetSpeculativeTypeInfo(
                    invocationContext.Syntax.SpanStart,
                    invocationExpressionSyntax,
                    SpeculativeBindingOption.BindAsExpression);

                if (typeInfo.ConvertedType.IsAssignableTo(conversionOperation.Type, invocationContext.SemanticModel.Compilation))
                {
                    // Add a simplifier annotation to the parent to remove no longer needed parenthesis.
                    if (castExpressionSyntax.Parent is ParenthesizedExpressionSyntax parenthesizedExpressionSyntax)
                    {
                        editor.ReplaceNode(
                            parenthesizedExpressionSyntax,
                            parenthesizedExpressionSyntax
                                .ReplaceNode(
                                    castExpressionSyntax,
                                    castExpressionSyntax.Expression
                                        .ReplaceNode(invocationContext.Syntax, invocationExpressionSyntax)
                                        .WithTriviaFrom(castExpressionSyntax))
                                .WithAdditionalAnnotations(Simplifier.Annotation));
                    }
                    else
                    {
                        editor.ReplaceNode(
                            castExpressionSyntax,
                            castExpressionSyntax.Expression
                                .ReplaceNode(invocationContext.Syntax, invocationExpressionSyntax)
                                .WithTriviaFrom(castExpressionSyntax));
                    }
                }
                else
                {
                    editor.ReplaceNode(invocationContext.Syntax, invocationExpressionSyntax);
                }
            }
            else
            {
                editor.ReplaceNode(invocationContext.Syntax, invocationExpressionSyntax);
            }

            return document.WithSyntaxRoot(editor.GetChangedRoot());
        }
    }
}
