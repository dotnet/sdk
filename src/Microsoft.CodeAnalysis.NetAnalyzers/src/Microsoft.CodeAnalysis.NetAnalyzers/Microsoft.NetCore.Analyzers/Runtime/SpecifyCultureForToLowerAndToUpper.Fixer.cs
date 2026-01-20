// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    public abstract class SpecifyCultureForToLowerAndToUpperFixerBase : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(SpecifyCultureForToLowerAndToUpperAnalyzer.RuleId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span);

            if (ShouldFix(node))
            {
                var generator = SyntaxGenerator.GetGenerator(context.Document);

                var title = MicrosoftNetCoreAnalyzersResources.SpecifyCurrentCulture;
                context.RegisterCodeFix(CodeAction.Create(title,
                                                         async ct => await SpecifyCurrentCultureAsync(context.Document, generator, root, node, ct).ConfigureAwait(false),
                                                         equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.SpecifyCurrentCulture)),
                                        context.Diagnostics);

                title = MicrosoftNetCoreAnalyzersResources.UseInvariantVersion;
                context.RegisterCodeFix(CodeAction.Create(title,
                                                         async ct => await UseInvariantVersionAsync(context.Document, generator, root, node).ConfigureAwait(false),
                                                         equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.UseInvariantVersion)),
                                        context.Diagnostics);
            }
        }

        protected abstract bool ShouldFix(SyntaxNode node);

        protected abstract Task<Document> SpecifyCurrentCultureAsync(Document document, SyntaxGenerator generator, SyntaxNode root, SyntaxNode node, CancellationToken cancellationToken);

        protected static SyntaxNode CreateCurrentCultureMemberAccess(SyntaxGenerator generator, SemanticModel model)
        {
            var cultureInfoType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemGlobalizationCultureInfo)!;
            return generator.MemberAccessExpression(
                generator.TypeExpressionForStaticMemberAccess(cultureInfoType),
                generator.IdentifierName("CurrentCulture"));
        }

        protected abstract Task<Document> UseInvariantVersionAsync(Document document, SyntaxGenerator generator, SyntaxNode root, SyntaxNode node);

        protected static string GetReplacementMethodName(string currentMethodName) => currentMethodName switch
        {
            SpecifyCultureForToLowerAndToUpperAnalyzer.ToLowerMethodName => "ToLowerInvariant",
            SpecifyCultureForToLowerAndToUpperAnalyzer.ToUpperMethodName => "ToUpperInvariant",
            _ => currentMethodName,
        };

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }
    }
}
