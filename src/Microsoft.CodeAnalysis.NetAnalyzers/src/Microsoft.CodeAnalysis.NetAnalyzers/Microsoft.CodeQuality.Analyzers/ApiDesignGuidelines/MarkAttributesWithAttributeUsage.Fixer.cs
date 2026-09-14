// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class MarkAttributesWithAttributeUsageFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(MarkAttributesWithAttributeUsageAnalyzer.RuleId);

        // Each nested action applies a different AttributeTargets value, so the fix-all pass has to be
        // told which one the user picked - DocumentBasedFixAllProvider hands over every diagnostic it
        // collected without filtering by the equivalence key.
        public override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(
                static fixAllContext => GetAttributeTargetValue(fixAllContext.CodeActionEquivalenceKey),
                static (document, diagnostic, editor, attributeTargetValue, cancellationToken) =>
                    attributeTargetValue is null
                        ? Task.CompletedTask
                        : AddAttributeUsageAttributeAsync(document, diagnostic, editor, attributeTargetValue, cancellationToken));

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (!semanticModel.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttributeUsageAttribute, out _) ||
                !semanticModel.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttributeTargets, out _))
            {
                return;
            }

            var document = context.Document;
            var diagnostics = context.Diagnostics;

            var applyAttributeTargetValues = Enum.GetValues(typeof(AttributeTargets))
                .Cast<AttributeTargets>()
                .Select(attributeTarget =>
                {
                    var attributeTargetValue = attributeTarget.ToString();
                    var title = $"{nameof(AttributeTargets)}.{attributeTargetValue}";

                    return CodeAction.Create(
                        title,
                        cancellationToken => SyntaxEditorFixAllProvider.ApplyFixesAsync(
                            document,
                            diagnostics,
                            (doc, diagnostic, editor, token) => AddAttributeUsageAttributeAsync(doc, diagnostic, editor, attributeTargetValue, token),
                            cancellationToken),
                        equivalenceKey: title);
                })
                .OrderBy(a => a.Title)
                .ToImmutableArray();

#pragma warning disable RS1010 // Provide an explicit value for EquivalenceKey - false positive
            context.RegisterCodeFix(
                CodeAction.Create(MicrosoftCodeQualityAnalyzersResources.MarkAttributesWithAttributeUsageCodeFix, applyAttributeTargetValues, isInlinable: false),
                diagnostics);
#pragma warning restore RS1010
        }

        /// <summary>
        /// Recovers the <see cref="AttributeTargets"/> value a nested action was registered for from its
        /// equivalence key, or <see langword="null"/> if the key names no such value.
        /// </summary>
        private static string? GetAttributeTargetValue(string? equivalenceKey)
        {
            const string Prefix = nameof(AttributeTargets) + ".";

            if (equivalenceKey is null || !equivalenceKey.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return null;
            }

            string value = equivalenceKey[Prefix.Length..];
            return Enum.TryParse(value, out AttributeTargets _) ? value : null;
        }

        private static async Task AddAttributeUsageAttributeAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor,
            string attributeTargetValue, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (!semanticModel.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttributeUsageAttribute, out var attributeUsageAttributeType) ||
                !semanticModel.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttributeTargets, out var attributeTargetsType))
            {
                return;
            }

            var nodeToFix = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);
            var attribute = editor.Generator.Attribute(editor.Generator.TypeExpression(attributeUsageAttributeType),
                new[] { editor.Generator.MemberAccessExpression(editor.Generator.TypeExpression(attributeTargetsType), attributeTargetValue) });
            editor.AddAttribute(nodeToFix, attribute);
        }
    }
}
