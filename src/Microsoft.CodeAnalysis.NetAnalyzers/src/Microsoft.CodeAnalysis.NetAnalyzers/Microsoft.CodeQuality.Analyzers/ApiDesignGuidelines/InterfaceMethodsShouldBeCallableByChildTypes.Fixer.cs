// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
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
    /// CA1033: Interface methods should be callable by child types
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class InterfaceMethodsShouldBeCallableByChildTypesFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(InterfaceMethodsShouldBeCallableByChildTypesAnalyzer.RuleId);

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SemanticModel semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode nodeToFix = root.FindNode(context.Span);
            if (nodeToFix == null)
            {
                return;
            }

            if (semanticModel.GetDeclaredSymbol(nodeToFix, context.CancellationToken) is not IMethodSymbol methodSymbol)
            {
                return;
            }

            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(context.Document);
            SyntaxNode declaration = generator.GetDeclaration(nodeToFix);
            if (declaration == null)
            {
                return;
            }

            IMethodSymbol? candidateToIncreaseVisibility = GetExistingNonVisibleAlternate(methodSymbol);
            if (candidateToIncreaseVisibility != null)
            {
                ISymbol symbolToChange;
                bool checkSetter = false;
                if (candidateToIncreaseVisibility.IsAccessorMethod())
                {
                    symbolToChange = candidateToIncreaseVisibility.AssociatedSymbol;
                    if (methodSymbol.AssociatedSymbol.Kind == SymbolKind.Property)
                    {
                        var originalProperty = (IPropertySymbol)methodSymbol.AssociatedSymbol;
                        checkSetter = originalProperty.SetMethod != null;
                    }
                }
                else
                {
                    symbolToChange = candidateToIncreaseVisibility;
                }

                if (symbolToChange != null)
                {
                    string title = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.InterfaceMethodsShouldBeCallableByChildTypesFix1, symbolToChange.Name);

                    context.RegisterCodeFix(new MyCodeAction(title,
                         async ct => await MakeProtectedAsync(context.Document, symbolToChange, checkSetter, ct).ConfigureAwait(false),
                         equivalenceKey: MicrosoftCodeQualityAnalyzersResources.InterfaceMethodsShouldBeCallableByChildTypesFix1),
                    context.Diagnostics);
                }
            }
            else
            {
                ISymbol symbolToChange = methodSymbol.IsAccessorMethod() ? methodSymbol.AssociatedSymbol : methodSymbol;
                if (symbolToChange != null)
                {
                    string title = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.InterfaceMethodsShouldBeCallableByChildTypesFix2, symbolToChange.Name);

                    context.RegisterCodeFix(new MyCodeAction(title,
                         async ct => await ChangeToPublicInterfaceImplementationAsync(context.Document, symbolToChange, ct).ConfigureAwait(false),
                         equivalenceKey: MicrosoftCodeQualityAnalyzersResources.InterfaceMethodsShouldBeCallableByChildTypesFix2),
                    context.Diagnostics);
                }
            }

            context.RegisterCodeFix(new MyCodeAction(string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.InterfaceMethodsShouldBeCallableByChildTypesFix3, methodSymbol.ContainingType.Name),
                     async ct => await MakeContainingTypeSealedAsync(context.Document, methodSymbol, ct).ConfigureAwait(false),
                         equivalenceKey: MicrosoftCodeQualityAnalyzersResources.InterfaceMethodsShouldBeCallableByChildTypesFix3),
                context.Diagnostics);
        }

        private static IMethodSymbol? GetExistingNonVisibleAlternate(IMethodSymbol methodSymbol)
        {
            foreach (IMethodSymbol interfaceMethod in methodSymbol.ExplicitInterfaceImplementations)
            {
                foreach (INamedTypeSymbol type in methodSymbol.ContainingType.GetBaseTypesAndThis())
                {
                    IMethodSymbol candidate = type.GetMembers(interfaceMethod.Name).OfType<IMethodSymbol>().FirstOrDefault(m => !m.Equals(methodSymbol));
                    if (candidate != null)
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static async Task<Document> MakeProtectedAsync(Document document, ISymbol symbolToChange, bool checkSetter, CancellationToken cancellationToken)
        {
            SymbolEditor editor = SymbolEditor.Create(document);

            ISymbol? getter = null;
            ISymbol? setter = null;
            if (symbolToChange.Kind == SymbolKind.Property)
            {
                var propertySymbol = (IPropertySymbol)symbolToChange;
                getter = propertySymbol.GetMethod;
                setter = propertySymbol.SetMethod;
            }

            await editor.EditAllDeclarationsAsync(symbolToChange, (docEditor, declaration) =>
            {
                docEditor.SetAccessibility(declaration, Accessibility.Protected);
            }, cancellationToken).ConfigureAwait(false);

            if (getter != null && getter.DeclaredAccessibility == Accessibility.Private)
            {
                await editor.EditAllDeclarationsAsync(getter, (docEditor, declaration) =>
                {
                    docEditor.SetAccessibility(declaration, Accessibility.NotApplicable);
                }, cancellationToken).ConfigureAwait(false);
            }

            if (checkSetter && setter != null && setter.DeclaredAccessibility == Accessibility.Private)
            {
                await editor.EditAllDeclarationsAsync(setter, (docEditor, declaration) =>
                {
                    docEditor.SetAccessibility(declaration, Accessibility.NotApplicable);
                }, cancellationToken).ConfigureAwait(false);
            }

            return editor.GetChangedDocuments().First();
        }

        private static async Task<Document> ChangeToPublicInterfaceImplementationAsync(Document document, ISymbol symbolToChange, CancellationToken cancellationToken)
        {
            SymbolEditor editor = SymbolEditor.Create(document);

            IEnumerable<ISymbol>? explicitImplementations = GetExplicitImplementations(symbolToChange);
            if (explicitImplementations == null)
            {
                return document;
            }

            await editor.EditAllDeclarationsAsync(symbolToChange, (docEditor, declaration) =>
            {
                SyntaxNode newDeclaration = declaration;
                foreach (ISymbol implementedMember in explicitImplementations)
                {
                    SyntaxNode interfaceTypeNode = docEditor.Generator.TypeExpression(implementedMember.ContainingType);
                    newDeclaration = docEditor.Generator.AsPublicInterfaceImplementation(newDeclaration, interfaceTypeNode);
                }

                docEditor.ReplaceNode(declaration, newDeclaration);
            }, cancellationToken).ConfigureAwait(false);

            return editor.GetChangedDocuments().First();
        }

        private static IEnumerable<ISymbol>? GetExplicitImplementations(ISymbol? symbol)
        {
            if (symbol == null)
            {
                return null;
            }

            return symbol.Kind switch
            {
                SymbolKind.Method => ((IMethodSymbol)symbol).ExplicitInterfaceImplementations,

                SymbolKind.Event => ((IEventSymbol)symbol).ExplicitInterfaceImplementations,

                SymbolKind.Property => ((IPropertySymbol)symbol).ExplicitInterfaceImplementations,

                _ => null,
            };
        }

        private static async Task<Document> MakeContainingTypeSealedAsync(Document document, IMethodSymbol methodSymbol, CancellationToken cancellationToken)
        {
            SymbolEditor editor = SymbolEditor.Create(document);

            await editor.EditAllDeclarationsAsync(methodSymbol.ContainingType, (docEditor, declaration) =>
            {
                DeclarationModifiers modifiers = docEditor.Generator.GetModifiers(declaration);
                docEditor.SetModifiers(declaration, modifiers + DeclarationModifiers.Sealed);
            }, cancellationToken).ConfigureAwait(false);

            return editor.GetChangedDocuments().First();
        }

        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }
    }
}
