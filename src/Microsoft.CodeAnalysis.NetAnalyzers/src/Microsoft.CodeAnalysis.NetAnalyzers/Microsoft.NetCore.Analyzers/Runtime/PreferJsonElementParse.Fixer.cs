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
    /// <summary>
    /// Fixer for <see cref="PreferJsonElementParse"/>.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class PreferJsonElementParseFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(PreferJsonElementParse.RuleId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            SemanticModel model = await doc.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode root = await doc.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode node = root.FindNode(context.Span, getInnermostNodeForTie: true);
            if (node == null)
            {
                return;
            }

            IOperation? operation = model.GetOperation(node, context.CancellationToken);
            if (operation is not IPropertyReferenceOperation propertyReference)
            {
                return;
            }

            // Verify this is the RootElement property access
            if (propertyReference.Property.Name != "RootElement")
            {
                return;
            }

            // Verify the instance is an invocation to JsonDocument.Parse
            if (propertyReference.Instance is not IInvocationOperation invocation)
            {
                return;
            }

            if (invocation.TargetMethod.Name != "Parse")
            {
                return;
            }

            string title = MicrosoftNetCoreAnalyzersResources.PreferJsonElementParseFix;
            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    createChangedDocument: async ct =>
                    {
                        DocumentEditor editor = await DocumentEditor.CreateAsync(doc, ct).ConfigureAwait(false);
                        SyntaxGenerator generator = editor.Generator;

                        // Get the JsonElement type
                        INamedTypeSymbol? jsonElementType = model.Compilation.GetOrCreateTypeByMetadataName("System.Text.Json.JsonElement");
                        if (jsonElementType == null)
                        {
                            return doc;
                        }

                        // Create the replacement: JsonElement.Parse(...)
                        // We need to use the same arguments that were passed to JsonDocument.Parse
                        var arguments = invocation.Arguments.Select(arg => arg.Syntax).ToArray();

                        SyntaxNode memberAccess = generator.MemberAccessExpression(
                            generator.TypeExpressionForStaticMemberAccess(jsonElementType),
                            "Parse");

                        SyntaxNode replacement = generator.InvocationExpression(memberAccess, arguments);

                        // Replace the entire property reference (JsonDocument.Parse(...).RootElement) with JsonElement.Parse(...)
                        editor.ReplaceNode(propertyReference.Syntax, replacement.WithTriviaFrom(propertyReference.Syntax));

                        return editor.GetChangedDocument();
                    },
                    equivalenceKey: title),
                context.Diagnostics);
        }
    }
}
