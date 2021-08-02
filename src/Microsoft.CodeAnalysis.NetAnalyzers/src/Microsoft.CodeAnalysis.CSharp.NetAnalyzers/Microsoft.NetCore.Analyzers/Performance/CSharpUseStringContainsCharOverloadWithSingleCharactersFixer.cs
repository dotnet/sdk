// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{

    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpUseStringContainsCharOverloadWithSingleCharactersFixer : UseStringContainsCharOverloadWithSingleCharactersCodeFix
    {
        protected override bool TryGetArgumentName(SyntaxNode violatingNode, out string argumentName)
        {
            argumentName = string.Empty;
            if (violatingNode is ArgumentSyntax argumentSyntax)
            {
                if (argumentSyntax.NameColon is null)
                    return false;

                argumentName = argumentSyntax.NameColon.Name.Identifier.ValueText;
                return true;
            }
            return false;
        }

        protected override bool TryGetLiteralValueFromNode(SyntaxNode violatingNode, out char charLiteral)
        {
            charLiteral = default;
            if (violatingNode is LiteralExpressionSyntax literalExpressionSyntax)
            {
                return TryGetCharFromLiteralExpressionSyntax(literalExpressionSyntax, out charLiteral);
            }
            else if (violatingNode is ArgumentSyntax argumentSyntax
                && argumentSyntax.Expression is LiteralExpressionSyntax containedLiteralExpressionSyntax)
            {
                return TryGetCharFromLiteralExpressionSyntax(containedLiteralExpressionSyntax, out charLiteral);
            }
            return false;

            static bool TryGetCharFromLiteralExpressionSyntax(LiteralExpressionSyntax sourceLiteralExpressionSyntax, out char parsedCharLiteral)
            {
                parsedCharLiteral = default;
                if (sourceLiteralExpressionSyntax.Token.Value is string sourceLiteralValue && char.TryParse(sourceLiteralValue, out parsedCharLiteral))
                {
                    return true;
                }
                return false;
            }
        }
    }
}
