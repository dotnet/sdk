// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>CA1837, CA1839, CA1840: Use Environment.ProcessId / ProcessPath / CurrentManagedThreadId</summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class UseEnvironmentMembersFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
            UseEnvironmentMembers.EnvironmentProcessIdRuleId,
            UseEnvironmentMembers.EnvironmentProcessPathRuleId,
            UseEnvironmentMembers.EnvironmentCurrentManagedThreadIdRuleId);

        // One title per rule, so a fix-all pass has to apply only the rule the user invoked it from.
        public sealed override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(
                static fixAllContext => fixAllContext.CodeActionEquivalenceKey,
                static (document, diagnostic, editor, equivalenceKey, cancellationToken) =>
                    equivalenceKey is null || equivalenceKey == GetTitle(diagnostic.Id)
                        ? ApplyFixAsync(document, diagnostic, editor, cancellationToken)
                        : Task.CompletedTask);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            SemanticModel model = await doc.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode root = await doc.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (root.FindNode(context.Span, getInnermostNodeForTie: true) is SyntaxNode node &&
                model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEnvironment, out _) &&
                model.GetOperation(node, context.CancellationToken) is IPropertyReferenceOperation)
            {
                string title = GetTitle(context.Diagnostics[0].Id);

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title,
                        cancellationToken => SyntaxEditorFixAllProvider.ApplyFixesAsync(doc, context.Diagnostics, ApplyFixAsync, cancellationToken),
                        equivalenceKey: title),
                    context.Diagnostics);
            }
        }

        private static string GetTitle(string ruleId) => ruleId switch
        {
            UseEnvironmentMembers.EnvironmentProcessIdRuleId => MicrosoftNetCoreAnalyzersResources.UseEnvironmentProcessIdFix,
            UseEnvironmentMembers.EnvironmentProcessPathRuleId => MicrosoftNetCoreAnalyzersResources.UseEnvironmentProcessPathFix,
            _ => MicrosoftNetCoreAnalyzersResources.UseEnvironmentCurrentManagedThreadIdFix,
        };

        private static string GetMemberName(string ruleId) => ruleId switch
        {
            UseEnvironmentMembers.EnvironmentProcessIdRuleId => "ProcessId",
            UseEnvironmentMembers.EnvironmentProcessPathRuleId => "ProcessPath",
            _ => "CurrentManagedThreadId",
        };

        private static async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            if (!model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEnvironment, out INamedTypeSymbol? environmentType) ||
                model.GetOperation(node, cancellationToken) is not IPropertyReferenceOperation)
            {
                return;
            }

            SyntaxNode replacement = editor.Generator.MemberAccessExpression(
                editor.Generator.TypeExpressionForStaticMemberAccess(environmentType),
                GetMemberName(diagnostic.Id));

            editor.ReplaceNode(node, replacement.WithTriviaFrom(node));
        }
    }
}