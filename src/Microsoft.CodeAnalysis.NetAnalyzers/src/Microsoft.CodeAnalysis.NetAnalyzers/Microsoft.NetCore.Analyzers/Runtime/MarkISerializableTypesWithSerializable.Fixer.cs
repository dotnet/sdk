// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2237: Mark ISerializable types with SerializableAttribute
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic, Name = "CA2237 CodeFix provider"), Shared]
    public sealed class MarkTypesWithSerializableFixer : SyntaxEditorBasedCodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(SerializationRulesDiagnosticAnalyzer.RuleCA2237Id);

        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            string title = MicrosoftNetCoreAnalyzersResources.AddSerializableAttributeCodeActionTitle;
            RegisterCodeFix(context, title, title);
            return Task.CompletedTask;
        }

        protected override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SyntaxNode node = editor.Generator.GetDeclaration(editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan));

            if (node == null)
            {
                return;
            }

            SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode attr = editor.Generator.Attribute(editor.Generator.TypeExpression(
                semanticModel.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSerializableAttribute)));
            editor.AddAttribute(node, attr);
        }
    }
}
