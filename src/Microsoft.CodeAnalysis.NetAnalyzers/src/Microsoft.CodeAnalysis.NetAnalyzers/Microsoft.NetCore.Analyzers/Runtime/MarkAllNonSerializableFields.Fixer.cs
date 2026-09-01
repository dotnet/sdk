// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            if (!editor.SemanticModel.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNonSerializedAttribute, out INamedTypeSymbol? nonSerializedAttributeType))
            {
                return document;
            }

            SyntaxNode attr = editor.Generator.Attribute(editor.Generator.TypeExpression(nonSerializedAttributeType));
            editor.AddAttribute(fieldNode, attr);
            return editor.GetChangedDocument();
        }

        private static async Task<Document> AddSerializableAttributeToTypeAsync(Document document, ITypeSymbol type, CancellationToken cancellationToken)
        {
            SymbolEditor editor = SymbolEditor.Create(document);
            await editor.EditOneDeclarationAsync(type, (docEditor, declaration) =>
            {
                if (!docEditor.SemanticModel.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSerializableAttribute, out INamedTypeSymbol? serializableAttributeType))
                {
                    return;
                }

                SyntaxNode serializableAttr = docEditor.Generator.Attribute(docEditor.Generator.TypeExpression(serializableAttributeType));
                docEditor.AddAttribute(declaration, serializableAttr);
            }, cancellationToken).ConfigureAwait(false);

            return editor.GetChangedDocuments().FirstOrDefault() ?? document;
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }
    }
}
