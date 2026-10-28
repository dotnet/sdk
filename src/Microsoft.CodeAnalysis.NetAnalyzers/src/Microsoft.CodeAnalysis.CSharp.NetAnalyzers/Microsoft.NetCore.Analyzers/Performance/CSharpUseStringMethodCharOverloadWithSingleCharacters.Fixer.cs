// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpUseStringMethodCharOverloadWithSingleCharactersFixer : UseStringMethodCharOverloadWithSingleCharactersFixer
    {
        protected override bool TryGetChar(SemanticModel model, SyntaxNode argumentListNode, out char c)
        {
            c = default;

            if (argumentListNode is not ArgumentListSyntax argumentList)
            {
                return false;
            }

            ArgumentSyntax? stringArgumentNode = null;
            foreach (var argument in argumentList.Arguments)
            {
                var argumentOperation = model.GetOperation(argument) as IArgumentOperation;
                if (argumentOperation?.Parameter != null && argumentOperation.Parameter.Ordinal == 0)
                {
                    stringArgumentNode = argument;
                    break;
                }
            }

            if (stringArgumentNode != null &&
                stringArgumentNode.Expression is LiteralExpressionSyntax containedLiteralExpressionSyntax)
            {
                return TryGetCharFromLiteralExpressionSyntax(containedLiteralExpressionSyntax, out c);
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

        protected override ImmutableArray<SyntaxNode> GetArguments(SyntaxNode argumentListNode)
            => ((ArgumentListSyntax)argumentListNode).Arguments.Cast<SyntaxNode>().ToImmutableArray();

        protected override SyntaxNode CreateArgumentList(IEnumerable<SyntaxNode> arguments)
            => SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments.Cast<ArgumentSyntax>()));
    }
}
