// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class UseStringEqualsOverStringCompareFixer : CodeFixProvider
    {
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var token = context.CancellationToken;
            var semanticModel = await document.GetSemanticModelAsync(token).ConfigureAwait(false);

            if (!UseStringEqualsOverStringCompare.RequiredSymbols.TryGetSymbols(semanticModel.Compilation, out var symbols))
                return;

            var root = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);

            if (semanticModel.GetOperation(root.FindNode(context.Span, getInnermostNodeForTie: true), token) is not IBinaryOperation operation)
                return;

            var selectors = UseStringEqualsOverStringCompare.GetSelectors(symbols);

            foreach (var selector in selectors)
            {
                if (selector.IsMatch(operation))
                {
                    var codeAction = CodeAction.Create(
                        Resx.UseStringEqualsOverStringCompareCodeFixTitle,
                        async token =>
                        {
                            var editor = await DocumentEditor.CreateAsync(document, token).ConfigureAwait(false);
                            var replacementNode = selector.GetReplacementExpression(operation, editor.Generator);
                            editor.ReplaceNode(operation.Syntax, replacementNode);
                            return editor.GetChangedDocument();
                        }, Resx.UseStringEqualsOverStringCompareCodeFixTitle);
                    context.RegisterCodeFix(codeAction, context.Diagnostics);
                    break;
                }
            }
        }

        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(UseStringEqualsOverStringCompare.RuleId);
    }
}
