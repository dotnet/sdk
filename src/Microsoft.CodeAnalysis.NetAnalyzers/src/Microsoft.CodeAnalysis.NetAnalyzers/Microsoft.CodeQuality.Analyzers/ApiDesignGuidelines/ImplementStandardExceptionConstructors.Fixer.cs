// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.CodeActions;
using Analyzer.Utilities;
using System.Composition;
using System.Collections.Generic;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1032: Implement standard exception constructors
    /// Cause: A type extends System.Exception and does not declare all the required constructors.
    /// Description: Exception types must implement the following constructors. Failure to provide the full set of constructors can make it difficult to correctly handle exceptions
    /// For CSharp, all possible  missing Constructors would be
    ///     public GoodException()
    ///     public GoodException(string)
    ///     public GoodException(string, Exception)
    /// For Basic, all possible  missing Constructors would be
    ///     Sub New()
    ///     Sub New(message As String)
    ///     Sub New(message As String, innerException As Exception)
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class ImplementStandardExceptionConstructorsFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ImplementStandardExceptionConstructorsAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // Fixes all occurrences within within Document, Project, or Solution
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            string title = MicrosoftCodeQualityAnalyzersResources.ImplementStandardExceptionConstructorsTitle;

            // Get syntax root node
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // Register fixer - pass in the collection of diagnostics, since there could be more than one for this diagnostic due to more than one of the required constructors missing
            context.RegisterCodeFix(CodeAction.Create(title, c => AddConstructorsAsync(context.Document, context.Diagnostics, root, c), equivalenceKey: title), context.Diagnostics.First());
        }

        private static async Task<Document> AddConstructorsAsync(Document document, IEnumerable<Diagnostic> diagnostics, SyntaxNode root, CancellationToken cancellationToken)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            SyntaxGenerator generator = editor.Generator;
            SemanticModel model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            CodeAnalysis.Text.TextSpan diagnosticSpan = diagnostics.First().Location.SourceSpan; // All the diagnostics are reported at the same location -- the name of the declared class -- so it doesn't matter which one we pick
            SyntaxNode node = root.FindNode(diagnosticSpan);
            SyntaxNode targetNode = editor.Generator.GetDeclaration(node, DeclarationKind.Class);
            if (model.GetDeclaredSymbol(targetNode, cancellationToken) is not INamedTypeSymbol typeSymbol)
            {
                return document;
            }

            foreach (Diagnostic diagnostic in diagnostics)
            {
                var missingCtorSignature = (ImplementStandardExceptionConstructorsAnalyzer.MissingCtorSignature)Enum.Parse(typeof(ImplementStandardExceptionConstructorsAnalyzer.MissingCtorSignature), diagnostic.Properties["Signature"]);

                switch (missingCtorSignature)
                {
                    case ImplementStandardExceptionConstructorsAnalyzer.MissingCtorSignature.CtorWithNoParameter:
                        // Add missing CtorWithNoParameter
                        SyntaxNode newConstructorNode1 = generator.ConstructorDeclaration(typeSymbol.Name, accessibility: Accessibility.Public);
                        editor.AddMember(targetNode, newConstructorNode1);
                        break;
                    case ImplementStandardExceptionConstructorsAnalyzer.MissingCtorSignature.CtorWithStringParameter:
                        // Add missing CtorWithStringParameter
                        SyntaxNode newConstructorNode2 = generator.ConstructorDeclaration(
                                                    containingTypeName: typeSymbol.Name,
                                                    parameters: new[]
                                                    {
                                                    generator.ParameterDeclaration("message", generator.TypeExpression(SpecialType.System_String))
                                                    },
                                                    accessibility: Accessibility.Public,
                                                    baseConstructorArguments: new[]
                                                    {
                                                    generator.Argument(generator.IdentifierName("message"))
                                                    });
                        editor.AddMember(targetNode, newConstructorNode2);
                        break;
                    case ImplementStandardExceptionConstructorsAnalyzer.MissingCtorSignature.CtorWithStringAndExceptionParameters:
                        // Add missing CtorWithStringAndExceptionParameters
                        SyntaxNode newConstructorNode3 = generator.ConstructorDeclaration(
                                                    containingTypeName: typeSymbol.Name,
                                                    parameters: new[]
                                                    {
                                                    generator.ParameterDeclaration("message", generator.TypeExpression(SpecialType.System_String)),
                                                    generator.ParameterDeclaration("innerException", generator.TypeExpression(editor.SemanticModel.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemException)))
                                                    },
                                                    accessibility: Accessibility.Public,
                                                    baseConstructorArguments: new[]
                                                    {
                                                    generator.Argument(generator.IdentifierName("message")),
                                                    generator.Argument(generator.IdentifierName("innerException"))
                                                    });
                        editor.AddMember(targetNode, newConstructorNode3);
                        break;
                }
            }

            return editor.GetChangedDocument();
        }
    }
}