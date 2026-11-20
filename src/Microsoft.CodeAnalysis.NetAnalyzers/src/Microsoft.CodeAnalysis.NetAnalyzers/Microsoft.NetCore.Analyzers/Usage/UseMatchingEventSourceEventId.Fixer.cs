// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Usage
{
    /// <summary>
    /// CA2269: <inheritdoc cref="MicrosoftNetCoreAnalyzersResources.UseMatchingEventSourceEventIdTitle"/>
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class UseMatchingEventSourceEventIdFixer : SyntaxEditorBasedCodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(UseMatchingEventSourceEventIdAnalyzer.RuleId);

        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            foreach (var diagnostic in context.Diagnostics)
            {
                if (TryGetExpectedEventId(diagnostic, out _))
                {
                    RegisterCodeFix(
                        context,
                        MicrosoftNetCoreAnalyzersResources.UseMatchingEventSourceEventIdCodeFixTitle,
                        nameof(MicrosoftNetCoreAnalyzersResources.UseMatchingEventSourceEventIdCodeFixTitle));
                    break;
                }
            }

            return Task.CompletedTask;
        }

        protected override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            if (TryGetExpectedEventId(diagnostic, out int expectedEventId))
            {
                SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
                editor.ReplaceNode(node, editor.Generator.LiteralExpression(expectedEventId).WithTriviaFrom(node));
            }

            return Task.CompletedTask;
        }

        private static bool TryGetExpectedEventId(Diagnostic diagnostic, out int expectedEventId)
        {
            expectedEventId = 0;
            return diagnostic.Properties.TryGetValue(UseMatchingEventSourceEventIdAnalyzer.ExpectedEventIdKey, out var value)
                && int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out expectedEventId);
        }
    }
}
