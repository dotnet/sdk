// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;
using RequiredSymbols = Microsoft.NetCore.Analyzers.Runtime.PreferAsSpanOverSubstring.RequiredSymbols;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    public abstract class PreferAsSpanOverSubstringFixer : CodeFixProvider
    {
        private const string SubstringStartIndexArgumentName = "startIndex";
        private const string AsSpanStartArgumentName = "start";
        private protected abstract SyntaxNode ReplaceInvocationMethodName(SyntaxNode memberInvocation, string newName);

        private protected abstract SyntaxNode ReplaceNamedArgumentName(SyntaxNode invocation, string oldName, string newName);

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(PreferAsSpanOverSubstring.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var token = context.CancellationToken;
            SyntaxNode root = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);
            SemanticModel model = await document.GetSemanticModelAsync(token).ConfigureAwait(false);
            var compilation = model.Compilation;

            if (root.FindNode(context.Span, getInnermostNodeForTie: true) is not SyntaxNode reportedNode || model.GetOperation(reportedNode, token) is not IInvocationOperation reportedInvocation)
                return;

            if (!RequiredSymbols.TryGetSymbols(compilation, out RequiredSymbols symbols))
                return;

            string title = MicrosoftNetCoreAnalyzersResources.PreferAsSpanOverSubstringTitle;
            var codeAction = CodeAction.Create(title, CreateChangedDocument, title);
            context.RegisterCodeFix(codeAction, context.Diagnostics);

            async Task<Document> CreateChangedDocument(CancellationToken token)
            {
                var editor = await DocumentEditor.CreateAsync(document, token).ConfigureAwait(false);

                foreach (var argument in reportedInvocation.Arguments)
                {
                    if (symbols.IsAnySubstringInvocation(argument.Value))
                    {
                        SyntaxNode asSpanInvocation = ReplaceInvocationMethodName(argument.Value.Syntax, nameof(MemoryExtensions.AsSpan));
                        asSpanInvocation = ReplaceNamedArgumentName(asSpanInvocation, SubstringStartIndexArgumentName, AsSpanStartArgumentName);
                        editor.ReplaceNode(argument.Value.Syntax, asSpanInvocation);
                    }
                }

                return editor.GetChangedDocument();
            }
        }

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
    }
}
