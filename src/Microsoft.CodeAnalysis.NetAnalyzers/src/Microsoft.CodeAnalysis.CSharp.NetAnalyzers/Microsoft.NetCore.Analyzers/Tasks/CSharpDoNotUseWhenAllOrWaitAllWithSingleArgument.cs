// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Tasks;

namespace Microsoft.NetCore.CSharp.Analyzers.Tasks
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpDoNotUseWhenAllOrWaitAllWithSingleArgument : DoNotUseWhenAllOrWaitAllWithSingleArgument
    {
        protected override bool IsSingleTaskArgument(IInvocationOperation invocation, INamedTypeSymbol taskType, INamedTypeSymbol task1Type)
        {
            if (invocation.Syntax is not InvocationExpressionSyntax invocationExpressionSyntax)
            {
                return false;
            }

            if (invocationExpressionSyntax.ArgumentList.Arguments.Count != 1)
            {
                return false;
            }

            var semanticModel = invocation.SemanticModel;
            var typeInfo = semanticModel.GetTypeInfo(invocationExpressionSyntax.ArgumentList.Arguments[0].Expression);

            // Check non generic task type
            if (typeInfo.Type == taskType)
            {
                return true;
            }

            // Check generic task type
            return typeInfo.Type is INamedTypeSymbol { Arity: 1 } namedTypeSymbol &&
                SymbolEqualityComparer.Default.Equals(namedTypeSymbol.ConstructedFrom, task1Type);
        }
    }
}
