// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public class PreferDictionaryContainsMethodsFixer : CodeFixProvider
    {
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            SyntaxNode root = await doc.GetSyntaxRootAsync().ConfigureAwait(false);
            SyntaxNode invocation = root.FindNode(context.Span);
            SyntaxNode containsMemberAccess = invocation.ChildNodes().First();
            SyntaxNode argumentList = invocation.ChildNodes().ElementAt(1);
            SyntaxNode keysOrValuesMemberAccess = containsMemberAccess.ChildNodes().First();
            SyntaxNode receiver = keysOrValuesMemberAccess.ChildNodes().First();
            SyntaxNode propertyIdentifier = keysOrValuesMemberAccess.ChildNodes().ElementAt(1);

            if (propertyIdentifier.GetText().ToString() == PreferDictionaryContainsMethods.KeysPropertyName)
            {
                var action = CodeAction.Create(
                    MicrosoftNetCoreAnalyzersResources.PreferDictionaryContainsKeyCodeFixTitle,
                    ReplaceKeysContainsWithContainsKeyAsync,
                    MicrosoftNetCoreAnalyzersResources.PreferDictionaryContainsKeyCodeFixTitle);
                context.RegisterCodeFix(action, context.Diagnostics);
            }
            else if (propertyIdentifier.GetText().ToString() == PreferDictionaryContainsMethods.ValuesPropertyName)
            {
                var action = CodeAction.Create(
                    MicrosoftNetCoreAnalyzersResources.PreferDictionaryContainsValueCodeFixTitle,
                    ReplaceValuesContainsWithContainsValueAsync,
                    MicrosoftNetCoreAnalyzersResources.PreferDictionaryContainsValueCodeFixTitle);
                context.RegisterCodeFix(action, context.Diagnostics);
            }

            async Task<Document> ReplaceKeysContainsWithContainsKeyAsync(CancellationToken ct)
            {
                var editor = await DocumentEditor.CreateAsync(doc, ct).ConfigureAwait(false);
                var containsKeyMemberAccess = editor.Generator.MemberAccessExpression(receiver, PreferDictionaryContainsMethods.ContainsKeyMethodName);
                var newInvocation = editor.Generator.InvocationExpression(containsKeyMemberAccess, argumentList.ChildNodes());
                editor.ReplaceNode(invocation, newInvocation);

                return editor.GetChangedDocument();
            }

            async Task<Document> ReplaceValuesContainsWithContainsValueAsync(CancellationToken ct)
            {
                var editor = await DocumentEditor.CreateAsync(doc, ct).ConfigureAwait(false);
                var containsValueMemberAccess = editor.Generator.MemberAccessExpression(receiver, PreferDictionaryContainsMethods.ContainsValueMethodName);
                var newInvocation = editor.Generator.InvocationExpression(containsValueMemberAccess, argumentList.ChildNodes());
                editor.ReplaceNode(invocation, newInvocation);

                return editor.GetChangedDocument();
            }
        }

        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(PreferDictionaryContainsMethods.RuleId);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
    }
}
