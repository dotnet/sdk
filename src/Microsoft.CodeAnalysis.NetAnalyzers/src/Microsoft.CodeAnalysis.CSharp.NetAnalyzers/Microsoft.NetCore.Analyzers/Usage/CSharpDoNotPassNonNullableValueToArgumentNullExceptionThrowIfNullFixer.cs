// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpDoNotPassNonNullableValueToArgumentNullExceptionThrowIfNullFixer : DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNullFixer<InvocationExpressionSyntax>
    {
        protected override void ReplaceWithNullableStructCheck(InvocationExpressionSyntax invocation, SyntaxNode statement, SyntaxEditor editor)
        {
            SyntaxGenerator generator = editor.Generator;
            var nullableStructExpression = invocation.ArgumentList.Arguments[0].Expression;
            var condition = generator.LogicalNotExpression(generator.MemberAccessExpression(nullableStructExpression, HasValue));
            var nameOfExpression = generator.NameOfExpression(nullableStructExpression);
            var argumentNullException = generator.ObjectCreationExpression(generator.IdentifierName(ArgumentNullException), nameOfExpression);
            var throwExpression = generator.ThrowStatement(argumentNullException);
            editor.ReplaceNode(statement, generator.IfStatement(condition, new[] { throwExpression }));
        }
    }
}