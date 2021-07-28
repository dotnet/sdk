// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
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
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1027: Mark enums with FlagsAttribute
    /// CA2217: Do not mark enums with FlagsAttribute
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class EnumWithFlagsAttributeFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EnumWithFlagsAttributeAnalyzer.RuleIdMarkEnumsWithFlags,
                                                                                   EnumWithFlagsAttributeAnalyzer.RuleIdDoNotMarkEnumsWithFlags);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SemanticModel model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            INamedTypeSymbol? flagsAttributeType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemFlagsAttribute);
            if (flagsAttributeType == null)
            {
                return;
            }

            foreach (var diagnostic in context.Diagnostics)
            {
                string fixTitle = diagnostic.Id == EnumWithFlagsAttributeAnalyzer.RuleIdMarkEnumsWithFlags ?
                                                        MicrosoftCodeQualityAnalyzersResources.MarkEnumsWithFlagsCodeFix :
                                                        MicrosoftCodeQualityAnalyzersResources.DoNotMarkEnumsWithFlagsCodeFix;
                context.RegisterCodeFix(new MyCodeAction(fixTitle,
                                             async ct => await AddOrRemoveFlagsAttributeAsync(context.Document, context.Span, diagnostic.Id, flagsAttributeType, ct).ConfigureAwait(false),
                                             equivalenceKey: fixTitle),
                                        diagnostic);
            }
        }

        private static async Task<Document> AddOrRemoveFlagsAttributeAsync(Document document, TextSpan span, string diagnosticId, INamedTypeSymbol flagsAttributeType, CancellationToken cancellationToken)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(span);

            SemanticModel model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode newEnumBlockSyntax = diagnosticId == EnumWithFlagsAttributeAnalyzer.RuleIdMarkEnumsWithFlags ?
                AddFlagsAttribute(editor.Generator, node, flagsAttributeType) :
                RemoveFlagsAttribute(editor.Generator, model, node, flagsAttributeType, cancellationToken);

            editor.ReplaceNode(node, newEnumBlockSyntax);
            return editor.GetChangedDocument();
        }

        private static SyntaxNode AddFlagsAttribute(SyntaxGenerator generator, SyntaxNode enumTypeSyntax, INamedTypeSymbol flagsAttributeType)
        {
            return generator.AddAttributes(enumTypeSyntax, generator.Attribute(generator.TypeExpression(flagsAttributeType)));
        }

        private static SyntaxNode RemoveFlagsAttribute(SyntaxGenerator generator, SemanticModel model, SyntaxNode enumTypeSyntax, INamedTypeSymbol flagsAttributeType, CancellationToken cancellationToken)
        {
            if (model.GetDeclaredSymbol(enumTypeSyntax, cancellationToken) is not INamedTypeSymbol enumType)
            {
                return enumTypeSyntax;
            }

            AttributeData flagsAttribute = enumType.GetAttributes().First(a => Equals(a.AttributeClass, flagsAttributeType));
            SyntaxNode attributeNode = flagsAttribute.ApplicationSyntaxReference.GetSyntax(cancellationToken);

            return generator.RemoveNode(enumTypeSyntax, attributeNode);
        }

        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }
    }
}
