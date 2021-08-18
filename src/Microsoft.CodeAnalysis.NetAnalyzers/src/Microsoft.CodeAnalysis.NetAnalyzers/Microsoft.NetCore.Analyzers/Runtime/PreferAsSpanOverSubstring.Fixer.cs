// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
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

        private protected abstract void ReplaceNonConditionalInvocationMethodName(SyntaxEditor editor, SyntaxNode memberInvocation, string newName);

        private protected abstract void ReplaceNamedArgumentName(SyntaxEditor editor, SyntaxNode invocation, string oldArgumentName, string newArgumentName);

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(PreferAsSpanOverSubstring.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var token = context.CancellationToken;
            SyntaxNode root = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);
            SemanticModel model = await document.GetSemanticModelAsync(token).ConfigureAwait(false);
            var compilation = model.Compilation;

            if (!RequiredSymbols.TryGetSymbols(compilation, out RequiredSymbols symbols) ||
                root.FindNode(context.Span, getInnermostNodeForTie: true) is not SyntaxNode reportedNode ||
                model.GetOperation(reportedNode, token) is not IInvocationOperation reportedInvocation)
            {
                return;
            }

            var bestCandidates = PreferAsSpanOverSubstring.GetBestSpanBasedOverloads(symbols, reportedInvocation, context.CancellationToken);

            //  We only apply the fix if there is an unambiguous best overload.
            if (bestCandidates.Length != 1)
                return;
            IMethodSymbol spanBasedOverload = bestCandidates[0];

            string title = MicrosoftNetCoreAnalyzersResources.PreferAsSpanOverSubstringCodefixTitle;
            var codeAction = CodeAction.Create(title, CreateChangedDocument, title);
            context.RegisterCodeFix(codeAction, context.Diagnostics);
            return;

            async Task<Document> CreateChangedDocument(CancellationToken token)
            {
                var editor = await DocumentEditor.CreateAsync(document, token).ConfigureAwait(false);

                foreach (var argument in reportedInvocation.Arguments)
                {
                    IOperation value = argument.Value.WalkDownConversion(c => c.IsImplicit);
                    IParameterSymbol newParameter = spanBasedOverload.Parameters[argument.Parameter.Ordinal];

                    //  Convert Substring invocations to equivalent AsSpan invocations.
                    if (symbols.IsAnySubstringInvocation(value) && SymbolEqualityComparer.Default.Equals(newParameter.Type, symbols.ReadOnlySpanOfCharType))
                    {
                        ReplaceNonConditionalInvocationMethodName(editor, value.Syntax, nameof(MemoryExtensions.AsSpan));
                        //  Ensure named Substring arguments get renamed to their equivalent AsSpan counterparts.
                        ReplaceNamedArgumentName(editor, value.Syntax, SubstringStartIndexArgumentName, AsSpanStartArgumentName);
                    }

                    //  Ensure named arguments on the original overload are renamed to their 
                    //  ordinal counterparts on the new overload.
                    string oldArgumentName = argument.Parameter.Name;
                    string newArgumentName = newParameter.Name;
                    ReplaceNamedArgumentName(editor, reportedInvocation.Syntax, oldArgumentName, newArgumentName);
                }

                //  Import System namespace if necessary.
                if (!IsMemoryExtensionsInScope(symbols, reportedInvocation))
                {
                    SyntaxNode withoutSystemImport = editor.GetChangedRoot();
                    SyntaxNode systemNamespaceImportStatement = editor.Generator.NamespaceImportDeclaration(nameof(System));
                    SyntaxNode withSystemImport = editor.Generator.AddNamespaceImports(withoutSystemImport, systemNamespaceImportStatement);
                    editor.ReplaceNode(editor.OriginalRoot, withSystemImport);
                }

                return editor.GetChangedDocument();
            }
        }

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        private static bool IsMemoryExtensionsInScope(in RequiredSymbols symbols, IInvocationOperation invocation)
        {
            var model = invocation.SemanticModel;
            int position = invocation.Syntax.SpanStart;
            const string name = nameof(MemoryExtensions);

            return model.LookupNamespacesAndTypes(position, name: name)
                .Contains(symbols.MemoryExtensionsType, SymbolEqualityComparer.Default);
        }
    }
}
