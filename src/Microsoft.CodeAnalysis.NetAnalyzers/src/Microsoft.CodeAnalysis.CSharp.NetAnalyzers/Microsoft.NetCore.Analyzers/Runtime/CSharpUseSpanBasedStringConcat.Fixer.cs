// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public sealed class CSharpUseSpanBasedStringConcatFixer : UseSpanBasedStringConcatFixer
    {
        private protected override SyntaxNode ReplaceInvocationMethodName(SyntaxGenerator generator, SyntaxNode invocationSyntax, string newName)
        {
            var memberAccessSyntax = (MemberAccessExpressionSyntax)((InvocationExpressionSyntax)invocationSyntax).Expression;
            var oldNameSyntax = memberAccessSyntax.Name;
            var newNameSyntax = generator.IdentifierName(newName).WithTriviaFrom(oldNameSyntax);
            return invocationSyntax.ReplaceNode(oldNameSyntax, newNameSyntax);
        }

        private protected override IOperation WalkDownBuiltInImplicitConversionOnConcatOperand(IOperation operand)
        {
            return UseSpanBasedStringConcat.CSharpWalkDownBuiltInImplicitConversionOnConcatOperand(operand);
        }

        private protected override bool IsNamedArgument(IArgumentOperation argumentOperation)
        {
            return argumentOperation.Syntax is ArgumentSyntax argumentSyntax && argumentSyntax.NameColon is not null;
        }
    }
}
