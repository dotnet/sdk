// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    internal sealed class CSharpUseVolatileReadWriteFixer : UseVolatileReadWriteFixer
    {
        protected override ImmutableArray<SyntaxNode> GetArguments(SyntaxNode invocationSyntax)
            => ImmutableArray.CreateRange<SyntaxNode>(((InvocationExpressionSyntax)invocationSyntax).ArgumentList.Arguments);

        protected override SyntaxNode WithParameterName(SyntaxNode argumentSyntax, string parameterName)
        {
            var argument = (ArgumentSyntax)argumentSyntax;

            return argument.NameColon is null ? argument : argument.WithNameColon(SyntaxFactory.NameColon(parameterName));
        }
    }
}