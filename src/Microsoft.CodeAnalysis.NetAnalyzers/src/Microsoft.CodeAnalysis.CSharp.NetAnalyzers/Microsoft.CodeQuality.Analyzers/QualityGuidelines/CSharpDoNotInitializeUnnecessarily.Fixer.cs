// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>CA1805: Do not initialize unnecessarily.</summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpDoNotInitializeUnnecessarilyFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DoNotInitializeUnnecessarilyAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            SyntaxNode root = await doc.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // Get the target syntax node from the incoming span.  For a field like:
            //     private string _value = null;
            // the node will be for the `= null;` portion.  For a property like:
            //     private string Value { get; } = "hello";
            // the node will be for the `= "hello"`.
            if (root.FindNode(context.Span) is SyntaxNode node)
            {
                string title = MicrosoftCodeQualityAnalyzersResources.DoNotInitializeUnnecessarilyFix;
                context.RegisterCodeFix(
                    new MyCodeAction(title,
                    async ct =>
                    {
                        // Simply delete the field or property initializer.
                        DocumentEditor editor = await DocumentEditor.CreateAsync(doc, ct).ConfigureAwait(false);
                        if (node.Parent is PropertyDeclarationSyntax prop)
                        {
                            // For a property, we also need to get rid of the semicolon that follows the initializer.
                            editor.ReplaceNode(prop, prop.WithInitializer(default).WithSemicolonToken(default).WithTrailingTrivia(prop.SemicolonToken.TrailingTrivia));
                        }
                        else
                        {
                            editor.RemoveNode(node);
                        }

                        // Return the new doc.
                        return editor.GetChangedDocument();
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