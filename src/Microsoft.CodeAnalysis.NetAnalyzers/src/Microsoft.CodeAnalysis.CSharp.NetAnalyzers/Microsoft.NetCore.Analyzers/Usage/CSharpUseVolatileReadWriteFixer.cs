// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    internal sealed class CSharpUseVolatileReadWriteFixer : UseVolatileReadWriteFixer
    {
        protected override SyntaxNode GetArgumentForVolatileReadCall(IArgumentOperation argument, IParameterSymbol volatileReadParameter)
        {
            var argumentSyntax = (ArgumentSyntax)argument.Syntax;
            if (argumentSyntax.NameColon is null)
            {
                return argumentSyntax;
            }

            return argumentSyntax.WithNameColon(SyntaxFactory.NameColon(volatileReadParameter.Name));
        }

        protected override IEnumerable<SyntaxNode> GetArgumentForVolatileWriteCall(ImmutableArray<IArgumentOperation> arguments, ImmutableArray<IParameterSymbol> volatileWriteParameters)
        {
            foreach (var argument in arguments)
            {
                var argumentSyntax = (ArgumentSyntax)argument.Syntax;
                if (argumentSyntax.NameColon is null)
                {
                    yield return argumentSyntax;
                }
                else
                {
                    var parameterName = volatileWriteParameters[argument.Parameter!.Ordinal].Name;
                    yield return argumentSyntax.WithNameColon(SyntaxFactory.NameColon(parameterName));
                }
            }
        }
    }
}