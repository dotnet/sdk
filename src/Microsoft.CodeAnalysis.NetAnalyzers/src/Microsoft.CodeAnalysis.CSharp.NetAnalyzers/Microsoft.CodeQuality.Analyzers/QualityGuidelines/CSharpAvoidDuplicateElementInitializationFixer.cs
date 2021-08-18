// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeQuality.Analyzers;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines;

namespace Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA2244: Do not duplicate indexed element initializations
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpAvoidDuplicateElementInitializationFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(AvoidDuplicateElementInitialization.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
            => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.FirstOrDefault();
            if (diagnostic?.AdditionalLocations.Count != 1)
            {
                return;
            }

            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root.FindNode(diagnostic.AdditionalLocations[0].SourceSpan) is not ExpressionSyntax elementInitializer ||
                elementInitializer.Parent is not InitializerExpressionSyntax objectInitializer)
            {
                return;
            }

            context.RegisterCodeFix(
                new MyCodeAction(
                    MicrosoftCodeQualityAnalyzersResources.RemoveRedundantElementInitializationCodeFixTitle,
                    _ => Task.FromResult(RemoveElementInitializer(elementInitializer, objectInitializer, root, context.Document))),
                context.Diagnostics);
        }

        private static Document RemoveElementInitializer(
            ExpressionSyntax elementInitializer,
            InitializerExpressionSyntax objectInitializer,
            SyntaxNode root,
            Document document)
        {
            var newElementInitializers = objectInitializer.Expressions.Remove(elementInitializer);
            var newRoot = root.ReplaceNode(objectInitializer, objectInitializer.WithExpressions(newElementInitializers));
            return document.WithSyntaxRoot(newRoot);
        }

        private sealed class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument)
                : base(title, createChangedDocument, equivalenceKey: title)
            {
            }
        }
    }
}
