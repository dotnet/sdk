// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1008: Enums should have zero value
    /// </summary>
    public abstract class EnumsShouldHaveZeroValueFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EnumsShouldHaveZeroValueAnalyzer.RuleId);

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
                SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
                SyntaxNode node = root.FindNode(context.Span);

                ISymbol? declaredSymbol = model.GetDeclaredSymbol(node, context.CancellationToken);
                if (declaredSymbol == null)
                {
                    continue;
                }

                string title;

                foreach (string customTag in diagnostic.Descriptor.CustomTags)
                {
                    switch (customTag)
                    {
                        case EnumsShouldHaveZeroValueAnalyzer.RuleRenameCustomTag:
                            title = MicrosoftCodeQualityAnalyzersResources.EnumsShouldZeroValueFlagsRenameCodeFix;
                            context.RegisterCodeFix(new MyCodeAction(title,
                                                        async ct => await GetUpdatedDocumentForRuleNameRenameAsync(context.Document, (IFieldSymbol)declaredSymbol, context.CancellationToken).ConfigureAwait(false),
                                                        equivalenceKey: title),
                                                    diagnostic);
                            return;
                        case EnumsShouldHaveZeroValueAnalyzer.RuleMultipleZeroCustomTag:
                            title = MicrosoftCodeQualityAnalyzersResources.EnumsShouldZeroValueFlagsMultipleZeroCodeFix;
                            context.RegisterCodeFix(new MyCodeAction(title,
                                                        async ct => await ApplyRuleNameMultipleZeroAsync(context.Document, (INamedTypeSymbol)declaredSymbol, context.CancellationToken).ConfigureAwait(false),
                                                        equivalenceKey: title),
                                                    diagnostic);
                            return;

                        case EnumsShouldHaveZeroValueAnalyzer.RuleNoZeroCustomTag:
                            title = MicrosoftCodeQualityAnalyzersResources.EnumsShouldZeroValueNotFlagsNoZeroValueCodeFix;
                            context.RegisterCodeFix(new MyCodeAction(title,
                                                        async ct => await ApplyRuleNameNoZeroValueAsync(context.Document, (INamedTypeSymbol)declaredSymbol, context.CancellationToken).ConfigureAwait(false),
                                                        equivalenceKey: title),
                                                    diagnostic);
                            return;
                    }
                }
            }
        }

        public override FixAllProvider? GetFixAllProvider()
        {
            // No trivial way to FixAll diagnostics for this code fix.
            return null;
        }

        private static SyntaxNode GetExplicitlyAssignedField(IFieldSymbol originalField, SyntaxNode declaration, SyntaxGenerator generator)
        {
            SyntaxNode originalInitializer = generator.GetExpression(declaration);
            if (originalInitializer != null || !originalField.HasConstantValue)
            {
                return declaration;
            }

            return generator.WithExpression(declaration, generator.LiteralExpression(originalField.ConstantValue));
        }

        private static async Task<Document> GetUpdatedDocumentForRuleNameRenameAsync(Document document, IFieldSymbol field, CancellationToken cancellationToken)
        {
            Solution newSolution = await CodeAnalysis.Rename.Renamer.RenameSymbolAsync(document.Project.Solution, field, "None", null, cancellationToken).ConfigureAwait(false);
            return newSolution.GetDocument(document.Id)!;
        }

        private static async Task<Document> ApplyRuleNameMultipleZeroAsync(Document document, INamedTypeSymbol enumType, CancellationToken cancellationToken)
        {
            // Diagnostic: Remove all members that have the value zero from '{0}' except for one member that is named 'None'.
            // Fix: Remove all members that have the value zero except for one member that is named 'None'.
            SymbolEditor editor = SymbolEditor.Create(document);

            bool needsNewZeroValuedNoneField = true;
            ISet<IFieldSymbol> set = EnumsShouldHaveZeroValueAnalyzer.GetZeroValuedFields(enumType).ToSet();

            bool makeNextFieldExplicit = false;
            foreach (IFieldSymbol field in enumType.GetMembers().Where(m => m.Kind == SymbolKind.Field))
            {
                bool isZeroValued = set.Contains(field);
                bool isZeroValuedNamedNone = isZeroValued && EnumsShouldHaveZeroValueAnalyzer.IsMemberNamedNone(field);

                if (!isZeroValued || isZeroValuedNamedNone)
                {
                    if (makeNextFieldExplicit)
                    {
                        await editor.EditOneDeclarationAsync(field, (e, d) => e.ReplaceNode(d, GetExplicitlyAssignedField(field, d, e.Generator)), cancellationToken).ConfigureAwait(false);
                        makeNextFieldExplicit = false;
                    }

                    if (isZeroValuedNamedNone)
                    {
                        needsNewZeroValuedNoneField = false;
                    }
                }
                else
                {
                    await editor.EditOneDeclarationAsync(field, (e, d) => e.RemoveNode(d), cancellationToken).ConfigureAwait(false); // removes the field declaration
                    makeNextFieldExplicit = true;
                }
            }

            if (needsNewZeroValuedNoneField)
            {
                await editor.EditOneDeclarationAsync(enumType, (e, d) => e.InsertMembers(d, 0, new[] { e.Generator.EnumMember("None") }), cancellationToken).ConfigureAwait(false);
            }

            return editor.GetChangedDocuments().First();
        }

        private static async Task<Document> ApplyRuleNameNoZeroValueAsync(Document document, INamedTypeSymbol enumType, CancellationToken cancellationToken)
        {
            SymbolEditor editor = SymbolEditor.Create(document);

            // remove any non-zero member named 'None'
            foreach (IFieldSymbol field in enumType.GetMembers().Where(m => m.Kind == SymbolKind.Field))
            {
                if (EnumsShouldHaveZeroValueAnalyzer.IsMemberNamedNone(field))
                {
                    await editor.EditOneDeclarationAsync(field, (e, d) => e.RemoveNode(d), cancellationToken).ConfigureAwait(false);
                }
            }

            // insert zero-valued member 'None' to top
            await editor.EditOneDeclarationAsync(enumType, (e, d) => e.InsertMembers(d, 0, new[] { e.Generator.EnumMember("None") }), cancellationToken).ConfigureAwait(false);

            return editor.GetChangedDocuments().First();
        }

        protected virtual SyntaxNode GetParentNodeOrSelfToFix(SyntaxNode nodeToFix)
        {
            return nodeToFix;
        }

        // Needed for Telemetry (https://github.com/dotnet/roslyn-analyzers/issues/192)
        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }
    }
}
