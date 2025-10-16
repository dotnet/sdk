// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using System.Threading;
using System.Threading.Tasks;
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
        protected override async Task<SyntaxNode> GetNewRootForNullableStructAsync(Document document, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var generator = editor.Generator;
            var nullableStructExpression = invocation.ArgumentList.Arguments[0].Expression;
            var condition = generator.LogicalNotExpression(generator.MemberAccessExpression(nullableStructExpression, HasValue));
            var nameOfExpression = generator.NameOfExpression(nullableStructExpression);
            var argumentNullException = generator.ObjectCreationExpression(generator.IdentifierName(ArgumentNullException), nameOfExpression);
            var throwExpression = generator.ThrowStatement(argumentNullException);
            var ifStatement = editor.Generator.IfStatement(condition, new[] { throwExpression });
            if (invocation.Parent is not null)
            {
                editor.ReplaceNode(invocation.Parent, ifStatement);
            }

            return editor.GetChangedRoot();
        }
    }
}
