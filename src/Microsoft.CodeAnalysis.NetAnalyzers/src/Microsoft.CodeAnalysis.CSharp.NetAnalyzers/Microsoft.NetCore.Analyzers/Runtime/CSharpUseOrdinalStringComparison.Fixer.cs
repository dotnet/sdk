// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public class CSharpUseOrdinalStringComparisonFixer : UseOrdinalStringComparisonFixerBase
    {
        protected override bool IsInArgumentContext(SyntaxNode node)
        {
            return node.IsKind(SyntaxKind.Argument) &&
                   ((ArgumentSyntax)node).Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression);
        }

        protected override Task<Document> FixArgumentAsync(Document document, SyntaxGenerator generator, SyntaxNode root, SyntaxNode argument)
        {
            if (((ArgumentSyntax)argument)?.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                // preserve the "IgnoreCase" suffix if present
                bool isIgnoreCase = memberAccess.Name.GetText().ToString().EndsWith(UseOrdinalStringComparisonAnalyzer.IgnoreCaseText, StringComparison.Ordinal);
                string newOrdinalText = isIgnoreCase ? UseOrdinalStringComparisonAnalyzer.OrdinalIgnoreCaseText : UseOrdinalStringComparisonAnalyzer.OrdinalText;
                SyntaxNode newIdentifier = generator.IdentifierName(newOrdinalText);
                MemberAccessExpressionSyntax newMemberAccess = memberAccess.WithName((SimpleNameSyntax)newIdentifier).WithAdditionalAnnotations(Formatter.Annotation);
                SyntaxNode newRoot = root.ReplaceNode(memberAccess, newMemberAccess);
                return Task.FromResult(document.WithSyntaxRoot(newRoot));
            }

            return Task.FromResult(document);
        }

        protected override bool IsInIdentifierNameContext(SyntaxNode node)
        {
            return node.IsKind(SyntaxKind.IdentifierName) &&
                   node?.Parent?.FirstAncestorOrSelf<InvocationExpressionSyntax>() != null;
        }

        protected override async Task<Document> FixIdentifierNameAsync(Document document, SyntaxGenerator generator, SyntaxNode root, SyntaxNode identifier, CancellationToken cancellationToken)
        {
            if (identifier?.Parent?.FirstAncestorOrSelf<InvocationExpressionSyntax>() is InvocationExpressionSyntax invokeParent)
            {
                SemanticModel model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                if (model.GetSymbolInfo((IdentifierNameSyntax)identifier!, cancellationToken).Symbol is IMethodSymbol methodSymbol && CanAddStringComparison(methodSymbol, model))
                {
                    // append a new StringComparison.Ordinal argument
                    SyntaxNode newArg = generator.Argument(CreateOrdinalMemberAccess(generator, model))
                        .WithAdditionalAnnotations(Formatter.Annotation);
                    InvocationExpressionSyntax newInvoke = invokeParent.AddArgumentListArguments((ArgumentSyntax)newArg).WithAdditionalAnnotations(Formatter.Annotation);
                    SyntaxNode newRoot = root.ReplaceNode(invokeParent, newInvoke);
                    return document.WithSyntaxRoot(newRoot);
                }
            }

            return document;
        }
    }
}
