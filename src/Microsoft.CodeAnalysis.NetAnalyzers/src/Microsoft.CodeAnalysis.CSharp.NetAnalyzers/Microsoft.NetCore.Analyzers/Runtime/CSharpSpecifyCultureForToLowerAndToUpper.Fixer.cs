// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
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

        protected override async Task<Document> SpecifyCurrentCultureAsync(Document document, SyntaxGenerator generator, SyntaxNode root, SyntaxNode node, CancellationToken cancellationToken)
        {
            if (node.IsKind(SyntaxKind.IdentifierName) && node.Parent?.FirstAncestorOrSelf<InvocationExpressionSyntax>() is InvocationExpressionSyntax invocation)
            {
                var model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                if (model.GetSymbolInfo((IdentifierNameSyntax)node, cancellationToken).Symbol is IMethodSymbol methodSymbol && methodSymbol.Parameters.Length == 0)
                {
                    var newArg = generator.Argument(CreateCurrentCultureMemberAccess(generator, model)).WithAdditionalAnnotations(Formatter.Annotation);
                    var newInvocation = invocation.AddArgumentListArguments((ArgumentSyntax)newArg).WithAdditionalAnnotations(Formatter.Annotation);
                    var newRoot = root.ReplaceNode(invocation, newInvocation);
                    return document.WithSyntaxRoot(newRoot);
                }
            }

            return document;
        }

        protected override Task<Document> UseInvariantVersionAsync(Document document, SyntaxGenerator generator, SyntaxNode root, SyntaxNode node)
        {
            if (node.IsKind(SyntaxKind.IdentifierName))
            {
                if (node.Parent is MemberAccessExpressionSyntax memberAccess)
                {
                    var replacementMethodName = GetReplacementMethodName(memberAccess.Name.Identifier.Text);
                    var newMemberAccess = memberAccess.WithName((SimpleNameSyntax)generator.IdentifierName(replacementMethodName)).WithAdditionalAnnotations(Formatter.Annotation);
                    var newRoot = root.ReplaceNode(memberAccess, newMemberAccess);
                    return Task.FromResult(document.WithSyntaxRoot(newRoot));
                }

                if (node.Parent is MemberBindingExpressionSyntax memberBinding)
                {
                    var replacementMethodName = GetReplacementMethodName(memberBinding.Name.Identifier.Text);
                    var newMemberBinding = memberBinding.WithName((SimpleNameSyntax)generator.IdentifierName(replacementMethodName)).WithAdditionalAnnotations(Formatter.Annotation);
                    var newRoot = root.ReplaceNode(memberBinding, newMemberBinding);
                    return Task.FromResult(document.WithSyntaxRoot(newRoot));
                }
            }

            return Task.FromResult(document);
        }
    }
}
