// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>CA1830: Prefer strongly-typed StringBuilder.Append overloads.</summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class PreferTypedStringBuilderAppendOverloadsFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(PreferTypedStringBuilderAppendOverloads.RuleId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            CancellationToken cancellationToken = context.CancellationToken;
            SyntaxNode root = await doc.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root.FindNode(context.Span) is SyntaxNode expression)
            {
                string title = MicrosoftNetCoreAnalyzersResources.PreferTypedStringBuilderAppendOverloadsRemoveToString;
                context.RegisterCodeFix(
                    new MyCodeAction(title,
                        async ct =>
                        {
                            SemanticModel model = await doc.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                            if (model.GetOperationWalkingUpParentChain(expression, cancellationToken) is IArgumentOperation arg &&
                                arg.Value is IInvocationOperation invoke &&
                                invoke.Instance?.Syntax is SyntaxNode replacement)
                            {
                                DocumentEditor editor = await DocumentEditor.CreateAsync(doc, ct).ConfigureAwait(false);
                                editor.ReplaceNode(expression, editor.Generator.Argument(replacement));
                                return editor.GetChangedDocument();
                            }

                            return doc;
                        },
                        equivalenceKey: title),
                    context.Diagnostics);
            }
        }

        // Needed for Telemetry (https://github.com/dotnet/roslyn-analyzers/issues/192)
        private sealed class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey) :
                base(title, createChangedDocument, equivalenceKey)
            {
            }
        }
    }
}