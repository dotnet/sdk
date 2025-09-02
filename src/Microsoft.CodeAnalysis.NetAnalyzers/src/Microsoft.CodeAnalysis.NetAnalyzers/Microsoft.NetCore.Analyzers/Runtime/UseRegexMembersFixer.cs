// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
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

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            SemanticModel model = await doc.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode root = await doc.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (root.FindNode(context.Span, getInnermostNodeForTie: true) is SyntaxNode node &&
                model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextRegularExpressionsRegex, out var regexType) &&
                model.GetOperation(node, context.CancellationToken) is IPropertyReferenceOperation operation &&
                operation.Instance is IInvocationOperation regexCall)
            {
                string title, memberName;
                switch (context.Diagnostics[0].Id)
                {
                    case UseRegexMembers.RegexIsMatchRuleId:
                        title = UseRegexIsMatchFix;
                        memberName = "IsMatch";
                        break;

                    case UseRegexMembers.RegexCountRuleId:
                        title = UseRegexCountFix;
                        memberName = "Count";
                        break;

                    default:
                        RoslynDebug.Assert(false, $"Unknown id {context.Diagnostics[0].Id}");
                        return;
                }

                context.RegisterCodeFix(
                    CodeAction.Create(title,
                    async cancellationToken =>
                    {
                        DocumentEditor editor = await DocumentEditor.CreateAsync(doc, cancellationToken).ConfigureAwait(false);

                        var replacement = editor.Generator.InvocationExpression(  // swap in new method name, dropping the subsequent parameter access
                            editor.Generator.MemberAccessExpression(
                                regexCall.Instance?.Syntax ?? editor.Generator.TypeExpressionForStaticMemberAccess(regexType),
                                memberName),
                            regexCall.Arguments.Select(arg => arg.Syntax)); // use the exact same arguments

                        editor.ReplaceNode(node, replacement.WithTriviaFrom(node));
                        return editor.GetChangedDocument();
                    },
                    equivalenceKey: title),
                    context.Diagnostics);
            }
        }
    }
}