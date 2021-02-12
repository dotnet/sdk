// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Tasks;

namespace Microsoft.NetCore.CSharp.Analyzers.Tasks
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpDoNotUseWhenAllOrWaitAllWithSingleArgumentFixer : DoNotUseWhenAllOrWaitAllWithSingleArgumentFixer
    {
        protected override SyntaxNode GetSingleArgumentSyntax(IInvocationOperation operation)
        {
            var invocationSyntax = (InvocationExpressionSyntax)operation.Syntax;
            return invocationSyntax.ArgumentList.Arguments.Single().Expression;
        }
    }
}
