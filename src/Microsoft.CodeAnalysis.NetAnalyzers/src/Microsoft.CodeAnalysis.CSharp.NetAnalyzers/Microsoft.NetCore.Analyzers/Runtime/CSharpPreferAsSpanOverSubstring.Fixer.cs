// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public sealed class CSharpPreferAsSpanOverSubstringFixer : PreferAsSpanOverSubstringFixer
    {
        private protected override void ReplaceNonConditionalInvocationMethodName(SyntaxEditor editor, SyntaxNode memberInvocation, string newName)
        {
            var cast = (InvocationExpressionSyntax)memberInvocation;
            var memberAccessSyntax = (MemberAccessExpressionSyntax)cast.Expression;
            var newNameSyntax = SyntaxFactory.IdentifierName(newName);
            editor.ReplaceNode(memberAccessSyntax.Name, newNameSyntax);
        }

        private protected override void ReplaceNamedArgumentName(SyntaxEditor editor, SyntaxNode invocation, string oldArgumentName, string newArgumentName)
        {
            var cast = (InvocationExpressionSyntax)invocation;
            var oldNameSyntax = cast.ArgumentList.Arguments
                .FirstOrDefault(x => x.NameColon is not null && x.NameColon.Name.Identifier.ValueText == oldArgumentName)?.NameColon.Name;
            if (oldNameSyntax is null)
                return;
            var newNameSyntax = SyntaxFactory.IdentifierName(newArgumentName);
            editor.ReplaceNode(oldNameSyntax, newNameSyntax);
        }
    }
}
