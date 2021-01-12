// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.NetCore.Analyzers.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.CodeActions;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public sealed class CSharpPreferDictionaryContainsMethodsFixer : PreferDictionaryContainsMethodsFixer
    {
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            SyntaxNode root = await doc.GetSyntaxRootAsync().ConfigureAwait(false);

            if (root.FindNode(context.Span) is not InvocationExpressionSyntax invocation)
                return;
            if (invocation.Expression is not MemberAccessExpressionSyntax containsMemberAccess)
                return;
            if (RemoveRoundBrackets(containsMemberAccess.Expression) is not MemberAccessExpressionSyntax keysOrValuesMemberAccess)
                return;

            if (keysOrValuesMemberAccess.Name.Identifier.ValueText == KeysPropertyName)
            {
                var action = CodeAction.Create(ContainsKeyCodeFixTitle, ReplaceWithContainsKeyAsync, ContainsKeyCodeFixTitle);
                context.RegisterCodeFix(action, context.Diagnostics);
            }
            else if (keysOrValuesMemberAccess.Name.Identifier.ValueText == ValuesPropertyName)
            {
                var action = CodeAction.Create(ContainsValueCodeFixTitle, ReplaceWithContainsValueAsync, ContainsValueCodeFixTitle);
                context.RegisterCodeFix(action, context.Diagnostics);
            }

            async Task<Document> ReplaceWithContainsKeyAsync(CancellationToken ct)
            {
                var editor = await DocumentEditor.CreateAsync(doc, ct).ConfigureAwait(false);
                var containsKeyMemberAccess = editor.Generator.MemberAccessExpression(keysOrValuesMemberAccess.Expression, ContainsKeyMethodName);
                var newInvocation = editor.Generator.InvocationExpression(containsKeyMemberAccess, invocation.ArgumentList.Arguments);
                editor.ReplaceNode(invocation, newInvocation);

                return editor.GetChangedDocument();
            }

            async Task<Document> ReplaceWithContainsValueAsync(CancellationToken ct)
            {
                var editor = await DocumentEditor.CreateAsync(doc, ct).ConfigureAwait(false);
                var containsValueMemberAccess = editor.Generator.MemberAccessExpression(keysOrValuesMemberAccess.Expression, ContainsValueMethodName);
                var newInvocation = editor.Generator.InvocationExpression(containsValueMemberAccess, invocation.ArgumentList.Arguments);
                editor.ReplaceNode(invocation, newInvocation);

                return editor.GetChangedDocument();
            }
        }

        private static SyntaxNode RemoveRoundBrackets(SyntaxNode possiblyPerenthecizedDictionaryPropertyAccessExpression)
        {
            SyntaxNode current = possiblyPerenthecizedDictionaryPropertyAccessExpression;
            while (current is ParenthesizedExpressionSyntax parenExpressionSyntax)
            {
                current = parenExpressionSyntax.Expression;
            }

            return current;
        }
    }
}
