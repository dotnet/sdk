// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
    /// <summary>
    /// CA2263: <inheritdoc cref="NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.PreferGenericOverloadsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpPreferGenericOverloadsAnalyzer : PreferGenericOverloadsAnalyzer
    {
        protected sealed override bool TryGetModifiedInvocationSyntax(RuntimeTypeInvocationContext invocationContext, [NotNullWhen(true)] out SyntaxNode? modifiedInvocationSyntax)
        {
            modifiedInvocationSyntax = GetModifiedInvocationSyntax(invocationContext);

            return modifiedInvocationSyntax is not null;
        }

        // Expose as internal static to allow the fixer to also call this method.
        internal static SyntaxNode? GetModifiedInvocationSyntax(RuntimeTypeInvocationContext invocationContext)
        {
            if (invocationContext.Syntax is not InvocationExpressionSyntax invocationSyntax)
            {
                return null;
            }

            var typeArgumentsSyntax = invocationContext.TypeArguments.Select(t => SyntaxFactory.ParseTypeName(t.ToDisplayString()));
            // Filter the source argument list rather than the normalized operations so every expanded params
            // argument remains in the replacement invocation.
            var otherArgumentsSyntax = invocationSyntax.ArgumentList.Arguments
                .Where(a => !invocationContext.IsTypeOfArgumentSyntax(a));
            var methodNameSyntax =
                SyntaxFactory.GenericName(
                    SyntaxFactory.Identifier(invocationContext.Method.Name),
                    SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(typeArgumentsSyntax)));
            var modifiedInvocationExpression = invocationSyntax.Expression;

            if (modifiedInvocationExpression is MemberAccessExpressionSyntax memberAccessExpressionSyntax)
            {
                modifiedInvocationExpression = memberAccessExpressionSyntax.WithName(methodNameSyntax);
            }
            else if (modifiedInvocationExpression is IdentifierNameSyntax identifierNameSyntax)
            {
                modifiedInvocationExpression = methodNameSyntax;
            }
            else
            {
                return null;
            }

            return invocationSyntax
                .WithExpression(modifiedInvocationExpression)
                .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(otherArgumentsSyntax)))
                .WithTriviaFrom(invocationSyntax);
        }
    }
}
