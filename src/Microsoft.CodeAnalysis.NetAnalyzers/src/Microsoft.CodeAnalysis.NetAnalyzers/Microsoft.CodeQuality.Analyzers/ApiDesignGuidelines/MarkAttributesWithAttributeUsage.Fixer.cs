// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class MarkAttributesWithAttributeUsageFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(MarkAttributesWithAttributeUsageAnalyzer.RuleId);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var nodeToFix = root.FindNode(context.Span);
            if (nodeToFix == null)
            {
                return;
            }

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (semanticModel == null ||
                !semanticModel.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttributeUsageAttribute, out var attributeUsageAttributeType) ||
                !semanticModel.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttributeTargets, out var attributeTargetsType))
            {
                return;
            }

            var title = MicrosoftCodeQualityAnalyzersResources.MarkAttributesWithAttributeUsageCodeFix;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    async ct => await AddAttributeUsageAttribute(context.Document, nodeToFix, attributeUsageAttributeType, attributeTargetsType, ct).ConfigureAwait(false),
                    equivalenceKey: title),
                context.Diagnostics);
        }

        private static async Task<Document> AddAttributeUsageAttribute(Document document, SyntaxNode nodeToFix, INamedTypeSymbol attributeUsageAttributeType,
            INamedTypeSymbol attributeTargetsType, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            var attribute = editor.Generator.Attribute(editor.Generator.TypeExpression(attributeUsageAttributeType),
                new[] { editor.Generator.MemberAccessExpression(editor.Generator.TypeExpression(attributeTargetsType), nameof(AttributeTargets.All)) });
            editor.AddAttribute(nodeToFix, attribute);

            return editor.GetChangedDocument();
        }
    }
}
