// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.ApiDiff.SyntaxRewriter;

internal class GlobalPrefixRemover : CSharpSyntaxRewriter
{
    public static readonly GlobalPrefixRemover Singleton = new();

    public override SyntaxNode? VisitAliasQualifiedName(AliasQualifiedNameSyntax node)
        => node.Alias.Identifier.IsKind(SyntaxKind.GlobalKeyword)
            ? node.Name.WithTriviaFrom(node)
            : base.VisitAliasQualifiedName(node);
}
