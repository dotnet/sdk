// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.NetCore.Analyzers.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.NetCore.Analyzers;
using Analyzer.Utilities.Extensions;

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
            if (containsMemberAccess.Expression.WalkDownParentheses() is not MemberAccessExpressionSyntax keysOrValuesMemberAccess)
                return;

            if (keysOrValuesMemberAccess.Name.Identifier.ValueText == PreferDictionaryContainsMethods.KeysPropertyName)
            {
                string codeFixTitle = MicrosoftNetCoreAnalyzersResources.PreferDictionaryContainsKeyCodeFixTitle;
                var action = CodeAction.Create(codeFixTitle, ct => ReplaceMethodNameAsync(PreferDictionaryContainsMethods.ContainsKeyMethodName, ct), codeFixTitle);
                context.RegisterCodeFix(action, context.Diagnostics);
            }
            else if (keysOrValuesMemberAccess.Name.Identifier.ValueText == PreferDictionaryContainsMethods.ValuesPropertyName)
            {
                string codeFixTitle = MicrosoftNetCoreAnalyzersResources.PreferDictionaryContainsValueCodeFixTitle;
                var action = CodeAction.Create(codeFixTitle, ct => ReplaceMethodNameAsync(PreferDictionaryContainsMethods.ContainsValueMethodName, ct), codeFixTitle);
                context.RegisterCodeFix(action, context.Diagnostics);
            }

            return;

            //  Local functions.

            async Task<Document> ReplaceMethodNameAsync(string methodName, CancellationToken ct)
            {
                var editor = await DocumentEditor.CreateAsync(doc, ct).ConfigureAwait(false);
                var containsMemberAccess = editor.Generator.MemberAccessExpression(keysOrValuesMemberAccess.Expression, methodName);
                var newInvocation = editor.Generator.InvocationExpression(containsMemberAccess, invocation.ArgumentList.Arguments);
                editor.ReplaceNode(invocation, newInvocation);

                return editor.GetChangedDocument();
            }
        }
    }
}
