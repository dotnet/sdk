// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.NetCore.Analyzers;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpDoNotCompareSpanToNullFixer : DoNotCompareSpanToNullFixer
    {
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var condition = root.FindNode(context.Span, getInnermostNodeForTie: true);
            if (condition is not BinaryExpressionSyntax binaryExpression)
            {
                return;
            }

            var useIsEmptyCodeAction = CodeAction.Create(
                MicrosoftNetCoreAnalyzersResources.DoNotCompareSpanToNullIsEmptyCodeFixTitle,
                _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(binaryExpression, MakeIsEmptyCheck(binaryExpression)))),
                MicrosoftNetCoreAnalyzersResources.DoNotCompareSpanToNullIsEmptyCodeFixTitle
            );
            context.RegisterCodeFix(useIsEmptyCodeAction, context.Diagnostics);
        }

        private static SyntaxNode MakeIsEmptyCheck(BinaryExpressionSyntax binaryExpression)
        {
            ExpressionSyntax memberAccess = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, GetComparatorExpression(binaryExpression), SyntaxFactory.IdentifierName(IsEmpty));
            if (binaryExpression.IsKind(SyntaxKind.NotEqualsExpression))
            {
                return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, memberAccess);
            }

            return memberAccess;
        }

        private static ExpressionSyntax GetComparatorExpression(BinaryExpressionSyntax binaryExpression)
        {
            return binaryExpression.Left.IsKind(SyntaxKind.NullLiteralExpression)
                   || binaryExpression.Left.IsKind(SyntaxKind.DefaultLiteralExpression)
                   || binaryExpression.Left.IsKind(SyntaxKind.DefaultExpression)
                ? binaryExpression.Right
                : binaryExpression.Left;
        }
    }
}