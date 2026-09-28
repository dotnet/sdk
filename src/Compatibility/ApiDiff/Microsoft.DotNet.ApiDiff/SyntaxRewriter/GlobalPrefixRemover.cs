// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.ApiDiff.SyntaxRewriter;

internal class GlobalPrefixRemover : CSharpSyntaxRewriter
{
    public static readonly GlobalPrefixRemover Singleton = new();

    private const string GlobalPrefix = "global";

    public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
    {
        if (node.Left is AliasQualifiedNameSyntax alias &&
            alias.Alias.Identifier.Text == GlobalPrefix)
        {
            node = SyntaxFactory.QualifiedName(alias.Name, node.Right).WithTriviaFrom(node);
        }
        return base.VisitQualifiedName(node);
    }
}
