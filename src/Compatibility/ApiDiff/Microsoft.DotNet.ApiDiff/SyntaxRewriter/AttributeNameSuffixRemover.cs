// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.ApiDiff.SyntaxRewriter;

internal class AttributeNameSuffixRemover : CSharpSyntaxRewriter
{
    public static readonly AttributeNameSuffixRemover Singleton = new();

    private const string AttributeSuffix = "Attribute";

    public override SyntaxNode? VisitAttribute(AttributeSyntax node)
    {
        if (node.Name is IdentifierNameSyntax identifierName &&
            identifierName.Identifier.Text.EndsWith(AttributeSuffix))
        {
            string newName = identifierName.Identifier.Text.Substring(0, identifierName.Identifier.Text.Length - AttributeSuffix.Length);
            IdentifierNameSyntax newIdentifier = SyntaxFactory.IdentifierName(newName).WithTriviaFrom(identifierName);
            return node.WithName(newIdentifier);
        }

        return base.VisitAttribute(node);
    }
}
