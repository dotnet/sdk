// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Analyzer.Utilities.Lightup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Runtime;
using NullableAnnotation = Analyzer.Utilities.Lightup.NullableAnnotation;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public sealed partial class CSharpForwardCancellationTokenToInvocationsFixer : ForwardCancellationTokenToInvocationsFixer<ArgumentSyntax>
    {
        protected override bool TryGetInvocation(
            SemanticModel model,
            SyntaxNode node,
            CancellationToken ct,
            [NotNullWhen(true)] out IInvocationOperation? invocation)
        {
            // If the method was invoked using nullability for the case of attempting to dereference a possibly null reference,
            // then the node.Parent.Parent is the actual invocation (and it will contain the dot as well)

            var operation = node.Parent.IsKind(SyntaxKind.MemberBindingExpression)
                ? model.GetOperation(node.Parent.Parent, ct)
                : model.GetOperation(node.Parent, ct);

            invocation = operation as IInvocationOperation;

            return invocation != null;
        }

        protected override bool IsArgumentNamed(IArgumentOperation argumentOperation)
        {
            return argumentOperation.Syntax is ArgumentSyntax argumentNode && argumentNode.NameColon != null;
        }

        protected override SyntaxNode GetConditionalOperationInvocationExpression(SyntaxNode invocationNode)
        {
            return ((InvocationExpressionSyntax)invocationNode).Expression;
        }

        protected override bool TryGetExpressionAndArguments(
            SyntaxNode invocationNode,
            [NotNullWhen(returnValue: true)] out SyntaxNode? expression,
            out ImmutableArray<ArgumentSyntax> arguments)
        {
            if (invocationNode is InvocationExpressionSyntax invocationExpression)
            {
                expression = invocationExpression.Expression;
                arguments = invocationExpression.ArgumentList.Arguments.ToImmutableArray();
                return true;
            }

            expression = null;
            arguments = ImmutableArray<ArgumentSyntax>.Empty;
            return false;
        }


        protected override SyntaxNode GetTypeSyntaxForArray(IArrayTypeSymbol type)
        {
            var typeName = TypeNameVisitor.GetTypeSyntaxForSymbol(type.ElementType);
            if (type.ElementType.IsReferenceType)
            {
                var additionalAnnotation = type.NullableAnnotation() switch
                {
                    NullableAnnotation.None => NullableSyntaxAnnotationEx.Oblivious,
                    NullableAnnotation.Annotated => NullableSyntaxAnnotationEx.AnnotatedOrNotAnnotated,
                    NullableAnnotation.NotAnnotated => NullableSyntaxAnnotationEx.AnnotatedOrNotAnnotated,
                    _ => null,
                };

                if (additionalAnnotation is not null)
                {
                    typeName = typeName.WithAdditionalAnnotations(additionalAnnotation);
                }
            }

            return typeName;
        }

        protected override IEnumerable<SyntaxNode> GetExpressions(ImmutableArray<ArgumentSyntax> newArguments)
        {
            return newArguments.Select(x => x.Expression);
        }
    }
}
