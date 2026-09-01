// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Composition;
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

        protected override void FixArgument(SyntaxNode argument, SyntaxEditor editor)
        {
            if (((ArgumentSyntax)argument).Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                return;
            }

            // preserve the "IgnoreCase" suffix if present
            bool isIgnoreCase = memberAccess.Name.GetText().ToString().EndsWith(UseOrdinalStringComparisonAnalyzer.IgnoreCaseText, StringComparison.Ordinal);
            string newOrdinalText = isIgnoreCase ? UseOrdinalStringComparisonAnalyzer.OrdinalIgnoreCaseText : UseOrdinalStringComparisonAnalyzer.OrdinalText;

            editor.ReplaceNode(
                memberAccess,
                (currentMemberAccess, generator) => ((MemberAccessExpressionSyntax)currentMemberAccess)
                    .WithName((SimpleNameSyntax)generator.IdentifierName(newOrdinalText))
                    .WithAdditionalAnnotations(Formatter.Annotation));
        }

        protected override bool IsInIdentifierNameContext(SyntaxNode node)
        {
            return node.IsKind(SyntaxKind.IdentifierName) &&
                   GetInvocation(node) is not null;
        }

        protected override SyntaxNode? GetInvocation(SyntaxNode identifier)
        {
            return identifier.Parent?.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        }

        protected override SyntaxNode AddArgument(SyntaxNode invocation, SyntaxNode argument)
        {
            return ((InvocationExpressionSyntax)invocation)
                .AddArgumentListArguments((ArgumentSyntax)argument)
                .WithAdditionalAnnotations(Formatter.Annotation);
        }
    }
}
