﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
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
    /// CA2237: Mark ISerializable types with SerializableAttribute
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic, Name = "CA2237 CodeFix provider"), Shared]
    public sealed class MarkTypesWithSerializableFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(SerializationRulesDiagnosticAnalyzer.RuleCA2237Id);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(context.Document);
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(context.Span);
            node = generator.GetDeclaration(node);
            if (node == null)
            {
                return;
            }

            string title = MicrosoftNetCoreAnalyzersResources.AddSerializableAttributeCodeActionTitle;
            context.RegisterCodeFix(CodeAction.Create(title,
                                        async ct => await AddSerializableAttributeAsync(context.Document, node, ct).ConfigureAwait(false),
                                        equivalenceKey: title),
                                    context.Diagnostics);
        }

        private static async Task<Document> AddSerializableAttributeAsync(Document document, SyntaxNode node, CancellationToken cancellationToken)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            SyntaxNode attr = editor.Generator.Attribute(editor.Generator.TypeExpression(
                editor.SemanticModel.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSerializableAttribute)));
            editor.AddAttribute(node, attr);
            return editor.GetChangedDocument();
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }
    }
}
