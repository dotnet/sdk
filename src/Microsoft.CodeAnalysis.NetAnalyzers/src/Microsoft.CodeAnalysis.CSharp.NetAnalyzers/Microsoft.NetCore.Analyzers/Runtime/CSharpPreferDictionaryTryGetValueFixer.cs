// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.NetCore.Analyzers.Runtime;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpPreferDictionaryTryGetValueFixer : PreferDictionaryTryGetValueFixer
    {
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.FirstOrDefault();
            var dictionaryAccessLocation = diagnostic?.AdditionalLocations[0];
            if (dictionaryAccessLocation is null)
            {
                return;
            }

            Document document = context.Document;
            SyntaxNode root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var dictionaryAccess = root.FindNode(dictionaryAccessLocation.SourceSpan, getInnermostNodeForTie: true);
            if (dictionaryAccess is not ElementAccessExpressionSyntax
                || root.FindNode(context.Span) is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax containsKeyAccess } containsKeyInvocation)
            {
                return;
            }

            var action = CodeAction.Create(PreferDictionaryTryGetValueCodeFixTitle, async ct =>
            {
                var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);
                var generator = editor.Generator;

                var tryGetValueAccess = generator.MemberAccessExpression(containsKeyAccess.Expression, TryGetValue);
                var keyArgument = containsKeyInvocation.ArgumentList.Arguments.FirstOrDefault();

                var outArgument = generator.Argument(RefKind.Out,
                    DeclarationExpression(
                        IdentifierName(Var),
                        SingleVariableDesignation(Identifier(Value))
                        )
                    );
                var tryGetValueInvocation = generator.InvocationExpression(tryGetValueAccess, keyArgument, outArgument);
                editor.ReplaceNode(containsKeyInvocation, tryGetValueInvocation);

                editor.ReplaceNode(dictionaryAccess, generator.IdentifierName(Value));

                return editor.GetChangedDocument();
            }, PreferDictionaryTryGetValueCodeFixTitle);

            context.RegisterCodeFix(action, context.Diagnostics);
        }
    }
}