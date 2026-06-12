// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis.CodeActions;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2235: Mark all non-serializable fields
    /// </summary>
    public abstract class MarkAllNonSerializableFieldsFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(SerializationRulesDiagnosticAnalyzer.RuleCA2235Id);

        protected abstract SyntaxNode? GetFieldDeclarationNode(SyntaxNode node);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(context.Span);
            SyntaxNode? fieldNode = GetFieldDeclarationNode(node);
            if (fieldNode == null)
            {
                return;
            }

            // Fix 1: Add a NonSerialized attribute to the field
            context.RegisterCodeFix(CodeAction.Create(MicrosoftNetCoreAnalyzersResources.AddNonSerializedAttributeCodeActionTitle,
                                        async ct => await AddNonSerializedAttributeAsync(context.Document, fieldNode, ct).ConfigureAwait(false),
                                        equivalenceKey: MicrosoftNetCoreAnalyzersResources.AddNonSerializedAttributeCodeActionTitle),
                                    context.Diagnostics);

            // Fix 2: If the type of the field is defined in source, then add the serializable attribute to the type.
            SemanticModel model = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var fieldSymbol = model.GetDeclaredSymbol(node, context.CancellationToken) as IFieldSymbol;
            ITypeSymbol? type = fieldSymbol?.Type;
            if (type != null && type.Locations.Any(l => l.IsInSource))
            {
                context.RegisterCodeFix(CodeAction.Create(MicrosoftNetCoreAnalyzersResources.AddSerializableAttributeCodeActionTitle,
                            async ct => await AddSerializableAttributeToTypeAsync(context.Document, type, ct).ConfigureAwait(false),
                            equivalenceKey: MicrosoftNetCoreAnalyzersResources.AddSerializableAttributeCodeActionTitle),
                        context.Diagnostics);
            }
        }

        private static async Task<Document> AddNonSerializedAttributeAsync(Document document, SyntaxNode fieldNode, CancellationToken cancellationToken)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            SyntaxNode attr = editor.Generator.Attribute(editor.Generator.TypeExpression(
                editor.SemanticModel.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNonSerializedAttribute)));
            editor.AddAttribute(fieldNode, attr);
            return editor.GetChangedDocument();
        }

        private static async Task<Document> AddSerializableAttributeToTypeAsync(Document document, ITypeSymbol type, CancellationToken cancellationToken)
        {
            SymbolEditor editor = SymbolEditor.Create(document);
            await editor.EditOneDeclarationAsync(type, (docEditor, declaration) =>
            {
                SyntaxNode serializableAttr = docEditor.Generator.Attribute(docEditor.Generator.TypeExpression(
                    docEditor.SemanticModel.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSerializableAttribute)));
                docEditor.AddAttribute(declaration, serializableAttr);
            }, cancellationToken).ConfigureAwait(false);

            return editor.GetChangedDocuments().First();
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }
    }
}
