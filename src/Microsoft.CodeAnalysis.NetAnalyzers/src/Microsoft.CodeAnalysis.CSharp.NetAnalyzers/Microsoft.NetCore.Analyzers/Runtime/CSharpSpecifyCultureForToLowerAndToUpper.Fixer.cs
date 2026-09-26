// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using System.Threading;
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
    public class CSharpSpecifyCultureForToLowerAndToUpperFixer : SpecifyCultureForToLowerAndToUpperFixerBase
    {
        protected override bool ShouldFix(SyntaxNode node)
        {
            return node.IsKind(SyntaxKind.IdentifierName) &&
                (node.Parent?.IsKind(SyntaxKind.SimpleMemberAccessExpression) == true || node.Parent?.IsKind(SyntaxKind.MemberBindingExpression) == true);
        }

        protected override SyntaxNode? GetNodeToSpecifyCurrentCultureOn(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
        {
            if (node is not IdentifierNameSyntax identifier ||
                identifier.Parent?.FirstAncestorOrSelf<InvocationExpressionSyntax>() is not InvocationExpressionSyntax invocation ||
                model.GetSymbolInfo(identifier, cancellationToken).Symbol is not IMethodSymbol { Parameters.Length: 0 })
            {
                return null;
            }

            return invocation;
        }

        protected override SyntaxNode SpecifyCurrentCulture(SyntaxNode currentNode, SyntaxNode currentCultureArgument, SyntaxGenerator generator)
        {
            return ((InvocationExpressionSyntax)currentNode)
                .AddArgumentListArguments((ArgumentSyntax)currentCultureArgument.WithAdditionalAnnotations(Formatter.Annotation))
                .WithAdditionalAnnotations(Formatter.Annotation);
        }

        protected override SyntaxNode? GetMemberAccessToMakeInvariant(SyntaxNode node)
        {
            if (!node.IsKind(SyntaxKind.IdentifierName))
            {
                return null;
            }

            return node.Parent is MemberAccessExpressionSyntax or MemberBindingExpressionSyntax ? node.Parent : null;
        }

        protected override SyntaxNode UseInvariantVersion(SyntaxNode currentMemberAccess, SyntaxGenerator generator)
        {
            if (currentMemberAccess is MemberAccessExpressionSyntax memberAccess)
            {
                var replacementMethodName = GetReplacementMethodName(memberAccess.Name.Identifier.Text);
                return memberAccess.WithName((SimpleNameSyntax)generator.IdentifierName(replacementMethodName)).WithAdditionalAnnotations(Formatter.Annotation);
            }

            var memberBinding = (MemberBindingExpressionSyntax)currentMemberAccess;
            var bindingReplacementMethodName = GetReplacementMethodName(memberBinding.Name.Identifier.Text);
            return memberBinding.WithName((SimpleNameSyntax)generator.IdentifierName(bindingReplacementMethodName)).WithAdditionalAnnotations(Formatter.Annotation);
        }
    }
}
