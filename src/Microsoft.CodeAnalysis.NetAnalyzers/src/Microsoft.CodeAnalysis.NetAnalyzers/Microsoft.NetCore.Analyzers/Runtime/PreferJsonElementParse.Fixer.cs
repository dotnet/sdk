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
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// Fixer for <see cref="PreferJsonElementParse"/>.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class PreferJsonElementParseFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(PreferJsonElementParse.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            SemanticModel model = await doc.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode root = await doc.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode node = root.FindNode(context.Span, getInnermostNodeForTie: true);

            if (GetParseInvocation(model, node, context.CancellationToken) is null ||
                model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextJsonJsonElement) is null)
            {
                return;
            }

            string title = MicrosoftNetCoreAnalyzersResources.PreferJsonElementParseFix;
            RegisterCodeFix(context, title, title);
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            if (GetParseInvocation(model, node, cancellationToken) is not IInvocationOperation invocation ||
                model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextJsonJsonElement) is not INamedTypeSymbol jsonElementType)
            {
                return;
            }

            ImmutableArray<SyntaxNode> arguments = invocation.Arguments.Select(argument => argument.Syntax).ToImmutableArray();

            foreach (SyntaxNode argument in arguments)
            {
                editor.TrackNode(argument);
            }

            // The arguments are carried over from inside the node being replaced, so they have to be read back
            // off the current node - an argument can itself hold a diagnostic this pass has already fixed.
            editor.ReplaceNode(node, (currentNode, generator) =>
            {
                SyntaxNode memberAccess = generator.MemberAccessExpression(
                    generator.TypeExpressionForStaticMemberAccess(jsonElementType),
                    "Parse");

                return generator.InvocationExpression(memberAccess, arguments.Select(argument => currentNode.GetCurrentNode(argument) ?? argument))
                    .WithTriviaFrom(currentNode);
            });
        }

        private static IInvocationOperation? GetParseInvocation(SemanticModel model, SyntaxNode node, CancellationToken cancellationToken)
        {
            return model.GetOperation(node, cancellationToken) is IPropertyReferenceOperation propertyReference &&
                propertyReference.Property.Name == "RootElement" &&
                propertyReference.Instance is IInvocationOperation invocation &&
                invocation.TargetMethod.Name == "Parse"
                ? invocation
                : null;
        }
    }
}
