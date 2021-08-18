// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.ImmutableCollections
{
    /// <summary>
    /// CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class DoNotCallToImmutableCollectionOnAnImmutableCollectionValueFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DoNotCallToImmutableCollectionOnAnImmutableCollectionValueAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.FirstOrDefault();
            if (diagnostic == null)
            {
                return;
            }

            var document = context.Document;
            var span = context.Span;
            var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var invocationNode = root.FindNode(span, getInnermostNodeForTie: true);
            if (invocationNode == null)
            {
                return;
            }

            var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (semanticModel.GetOperation(invocationNode, context.CancellationToken) is not IInvocationOperation invocationOperation ||
                !DoNotCallToImmutableCollectionOnAnImmutableCollectionValueAnalyzer.ToImmutableMethodNames.Contains(invocationOperation.TargetMethod.Name))
            {
                return;
            }

            var title = MicrosoftNetCoreAnalyzersResources.RemoveRedundantCall;

            context.RegisterCodeFix(new MyCodeAction(title,
                                        async cancellationToken => await RemoveRedundantCallAsync(document, root, invocationNode, invocationOperation).ConfigureAwait(false),
                                        equivalenceKey: title),
                                    diagnostic);
        }

        private static Task<Document> RemoveRedundantCallAsync(Document document, SyntaxNode root, SyntaxNode invocationNode, IInvocationOperation invocationOperation)
        {
            var instance = invocationOperation.GetInstanceSyntax().WithTriviaFrom(invocationNode);
            var newRoot = root.ReplaceNode(invocationNode, instance);
            var newDocument = document.WithSyntaxRoot(newRoot);
            return Task.FromResult(newDocument);
        }

        // Needed for Telemetry (https://github.com/dotnet/roslyn-analyzers/issues/192)
        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }
    }
}