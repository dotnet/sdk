// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public sealed class CSharpUseSpanBasedStringConcatFixer : UseSpanBasedStringConcatFixer
    {
        private protected override SyntaxNode ReplaceInvocationMethodName(SyntaxGenerator generator, SyntaxNode invocationSyntax, string newName)
        {
            var memberAccessSyntax = (MemberAccessExpressionSyntax)((InvocationExpressionSyntax)invocationSyntax).Expression;
            var oldNameSyntax = memberAccessSyntax.Name;
            var newNameSyntax = generator.IdentifierName(newName).WithTriviaFrom(oldNameSyntax);
            return invocationSyntax.ReplaceNode(oldNameSyntax, newNameSyntax);
        }

        private protected override SyntaxToken GetOperatorToken(IBinaryOperation binaryOperation)
        {
            var syntax = (BinaryExpressionSyntax)binaryOperation.Syntax;
            return syntax.OperatorToken;
        }

        private protected override bool IsSystemNamespaceImported(IReadOnlyList<SyntaxNode> namespaceImports)
        {
            foreach (var import in namespaceImports)
            {
                if (import is UsingDirectiveSyntax { Name: IdentifierNameSyntax { Identifier: { ValueText: nameof(System) } } })
                    return true;
            }
            return false;
        }

        private protected override bool IsNamedArgument(IArgumentOperation argument)
        {
            var node = (ArgumentSyntax)argument.Syntax;
            return node.NameColon is not null;
        }

        private protected override SyntaxNode CreateConditionalToStringInvocation(SyntaxNode receiverExpression)
        {
            var expression = (ExpressionSyntax)receiverExpression;
            var memberBindingExpression = SyntaxFactory.MemberBindingExpression(SyntaxFactory.IdentifierName(ToStringName));
            var toStringInvocationExpression = SyntaxFactory.InvocationExpression(memberBindingExpression);
            return SyntaxFactory.ConditionalAccessExpression(expression, toStringInvocationExpression);
        }
    }
}
