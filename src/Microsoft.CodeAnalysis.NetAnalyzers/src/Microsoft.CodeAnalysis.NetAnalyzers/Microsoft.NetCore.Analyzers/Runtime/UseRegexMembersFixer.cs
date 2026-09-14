// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1874: <inheritdoc cref="UseRegexIsMatchMessage"/>
    /// CA1875: <inheritdoc cref="UseRegexCountMessage"/>
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class UseRegexMembersFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
            UseRegexMembers.RegexIsMatchRuleId,
            UseRegexMembers.RegexCountRuleId);

        // The two rules carry different fix titles, and so different equivalence keys, which
        // SyntaxEditorFixAllProvider does not filter on - so the state is the key to apply.
        public sealed override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(context => context.CodeActionEquivalenceKey, ApplyFixAsync);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            SemanticModel model = await doc.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode root = await doc.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode node = root.FindNode(context.Span, getInnermostNodeForTie: true);

            if (GetRegexCall(model, node, context.CancellationToken) is null ||
                GetMemberName(context.Diagnostics[0].Id) is null ||
                GetTitle(context.Diagnostics[0].Id) is not string title)
            {
                return;
            }

            ImmutableArray<Diagnostic> diagnostics = context.Diagnostics;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken => SyntaxEditorFixAllProvider.ApplyFixesAsync(
                        doc,
                        diagnostics,
                        (document, diagnostic, editor, token) => ApplyFixAsync(document, diagnostic, editor, title, token),
                        cancellationToken),
                    equivalenceKey: title),
                diagnostics);
        }

        private static async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, string? equivalenceKey, CancellationToken cancellationToken)
        {
            if (equivalenceKey is not null && GetTitle(diagnostic.Id) != equivalenceKey)
            {
                return;
            }

            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            if (GetRegexCall(model, node, cancellationToken) is not IInvocationOperation regexCall ||
                GetMemberName(diagnostic.Id) is not string memberName ||
                !model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextRegularExpressionsRegex, out INamedTypeSymbol? regexType))
            {
                return;
            }

            SyntaxNode? instance = regexCall.Instance?.Syntax;
            ImmutableArray<SyntaxNode> arguments = regexCall.Arguments.Select(argument => argument.Syntax).ToImmutableArray();

            if (instance is not null)
            {
                editor.TrackNode(instance);
            }

            foreach (SyntaxNode argument in arguments)
            {
                editor.TrackNode(argument);
            }

            // The receiver and the arguments are carried over from inside the node being replaced, so they have
            // to be read back off the current node - either can itself hold a diagnostic already fixed here.
            editor.ReplaceNode(node, (currentNode, generator) =>
            {
                SyntaxNode target = instance is null
                    ? generator.TypeExpressionForStaticMemberAccess(regexType)
                    : currentNode.GetCurrentNode(instance) ?? instance;

                // Swap in the new member name, dropping the subsequent property access, and keep the arguments.
                return generator.InvocationExpression(
                    generator.MemberAccessExpression(target, memberName),
                    arguments.Select(argument => currentNode.GetCurrentNode(argument) ?? argument))
                    .WithTriviaFrom(currentNode);
            });
        }

        private static IInvocationOperation? GetRegexCall(SemanticModel model, SyntaxNode node, CancellationToken cancellationToken)
        {
            return model.GetOperation(node, cancellationToken) is IPropertyReferenceOperation operation &&
                operation.Instance is IInvocationOperation regexCall
                ? regexCall
                : null;
        }

        private static string? GetTitle(string ruleId) => ruleId switch
        {
            UseRegexMembers.RegexIsMatchRuleId => UseRegexIsMatchFix,
            UseRegexMembers.RegexCountRuleId => UseRegexCountFix,
            _ => null,
        };

        private static string? GetMemberName(string ruleId) => ruleId switch
        {
            UseRegexMembers.RegexIsMatchRuleId => "IsMatch",
            UseRegexMembers.RegexCountRuleId => "Count",
            _ => null,
        };
    }
}