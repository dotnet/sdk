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
            if (!semanticModel.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttributeUsageAttribute, out var attributeUsageAttributeType) ||
                !semanticModel.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttributeTargets, out var attributeTargetsType))
            {
                return;
            }

            var applyAttributeTargetValues = Enum.GetValues(typeof(AttributeTargets))
                .Cast<AttributeTargets>()
                .Select(attributeTarget =>
                {
                    var attributeTargetValue = attributeTarget.ToString();
                    var title = $"{nameof(AttributeTargets)}.{attributeTargetValue}";

                    return CodeAction.Create(
                        title,
                        async ct => await AddAttributeUsageAttribute(context.Document, nodeToFix, attributeUsageAttributeType, attributeTargetsType, attributeTargetValue, ct).ConfigureAwait(false),
                        equivalenceKey: title);
                })
                .OrderBy(a => a.Title)
                .ToImmutableArray();

#pragma warning disable RS1010 // Provide an explicit value for EquivalenceKey - false positive
            context.RegisterCodeFix(
                CodeAction.Create(MicrosoftCodeQualityAnalyzersResources.MarkAttributesWithAttributeUsageCodeFix, applyAttributeTargetValues, isInlinable: false),
                context.Diagnostics);
#pragma warning restore RS1010
        }

        private static async Task<Document> AddAttributeUsageAttribute(Document document, SyntaxNode nodeToFix, INamedTypeSymbol attributeUsageAttributeType,
            INamedTypeSymbol attributeTargetsType, string attributeTargetValue, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            var attribute = editor.Generator.Attribute(editor.Generator.TypeExpression(attributeUsageAttributeType),
                new[] { editor.Generator.MemberAccessExpression(editor.Generator.TypeExpression(attributeTargetsType), attributeTargetValue) });
            editor.AddAttribute(nodeToFix, attribute);

            return editor.GetChangedDocument();
        }
    }
}
