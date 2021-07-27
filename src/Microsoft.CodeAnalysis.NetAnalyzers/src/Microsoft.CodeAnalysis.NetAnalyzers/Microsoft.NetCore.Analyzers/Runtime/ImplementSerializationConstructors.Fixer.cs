// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2229: Implement serialization constructors.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic, Name = "CA2229 CodeFix provider"), Shared]
    public sealed class ImplementSerializationConstructorsFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(SerializationRulesDiagnosticAnalyzer.RuleCA2229Id);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(context.Span);
            SemanticModel model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            ISymbol symbol = model.GetDeclaredSymbol(node, context.CancellationToken);

            if (symbol == null)
            {
                return;
            }

            INamedTypeSymbol? notImplementedExceptionType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNotImplementedException);
            if (notImplementedExceptionType == null)
            {
                return;
            }

            // There was no constructor and so the diagnostic was on the type. Generate a serialization ctor.
            string title = MicrosoftNetCoreAnalyzersResources.ImplementSerializationConstructorsCodeActionTitle;
            if (symbol.Kind == SymbolKind.NamedType)
            {
                context.RegisterCodeFix(new MyCodeAction(title,
                     async ct => await GenerateConstructor(context.Document, node, (INamedTypeSymbol)symbol, notImplementedExceptionType, ct).ConfigureAwait(false),
                     equivalenceKey: title),
                context.Diagnostics);
            }
            // There is a serialization constructor but with incorrect accessibility. Set that right.
            else if (symbol.Kind == SymbolKind.Method)
            {
                context.RegisterCodeFix(new MyCodeAction(title,
                     async ct => await SetAccessibility(context.Document, (IMethodSymbol)symbol, ct).ConfigureAwait(false),
                     equivalenceKey: title),
                context.Diagnostics);
            }
        }

        private static async Task<Document> GenerateConstructor(Document document, SyntaxNode node, INamedTypeSymbol typeSymbol, INamedTypeSymbol notImplementedExceptionType, CancellationToken cancellationToken)
        {
            SymbolEditor editor = SymbolEditor.Create(document);

            await editor.EditOneDeclarationAsync(typeSymbol, node.GetLocation(), (docEditor, declaration) =>
            {
                SyntaxGenerator generator = docEditor.Generator;
                SyntaxNode throwStatement = generator.ThrowStatement(generator.ObjectCreationExpression(generator.TypeExpression(notImplementedExceptionType)));
                SyntaxNode ctorDecl = generator.ConstructorDeclaration(
                                    typeSymbol.Name,
                                    new[]
                                    {
                                            generator.ParameterDeclaration("serializationInfo", generator.TypeExpression(docEditor.SemanticModel.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationSerializationInfo))),
                                            generator.ParameterDeclaration("streamingContext", generator.TypeExpression(docEditor.SemanticModel.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationStreamingContext)))
                                    },
                                    typeSymbol.IsSealed ? Accessibility.Private : Accessibility.Protected,
                                    statements: new[] { throwStatement });

                docEditor.AddMember(declaration, ctorDecl);
            }, cancellationToken).ConfigureAwait(false);

            return editor.GetChangedDocuments().First();
        }

        private static async Task<Document> SetAccessibility(Document document, IMethodSymbol methodSymbol, CancellationToken cancellationToken)
        {
            SymbolEditor editor = SymbolEditor.Create(document);

            // This would be constructor and can have only one definition.
            Debug.Assert(methodSymbol.IsConstructor() && methodSymbol.DeclaringSyntaxReferences.HasExactly(1));
            await editor.EditOneDeclarationAsync(methodSymbol, (docEditor, declaration) =>
            {
                Accessibility newAccessibility = methodSymbol.ContainingType.IsSealed ? Accessibility.Private : Accessibility.Protected;
                docEditor.SetAccessibility(declaration, newAccessibility);
            }, cancellationToken).ConfigureAwait(false);

            return editor.GetChangedDocuments().First();
        }

        // Needed for Telemetry (https://github.com/dotnet/roslyn-analyzers/issues/192)
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
