// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.NetCore.Analyzers.Runtime;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpForwardCancellationTokenToInvocationsAnalyzer : ForwardCancellationTokenToInvocationsAnalyzer
    {
        protected override SyntaxNode? GetInvocationMethodNameNode(SyntaxNode invocationNode)
        {
            if (invocationNode is InvocationExpressionSyntax invocationExpression)
            {
                if (invocationExpression.Expression is MemberBindingExpressionSyntax memberBindingExpression)
                {
                    // When using nullability features, specifically attempting to dereference possible null references,
                    // the dot becomes part of the member invocation expression, so we need to return just the name,
                    // so that the diagnostic gets properly returned in the method name only.
                    return memberBindingExpression.Name;
                }
                return invocationExpression.Expression;
            }
            return null;
        }
        protected override bool ArgumentsImplicitOrNamed(INamedTypeSymbol cancellationTokenType, ImmutableArray<IArgumentOperation> arguments)
        {
            return arguments.Any(a =>
                (a.IsImplicit && !a.Parameter.Type.Equals(cancellationTokenType)) ||
                (a.Syntax is ArgumentSyntax argumentNode && argumentNode.NameColon != null));
        }
    }
}
