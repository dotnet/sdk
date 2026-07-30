// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Analyzer.Utilities;
using System.Composition;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis.NetAnalyzers;

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
    public sealed class ImplementStandardExceptionConstructorsFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(ImplementStandardExceptionConstructorsAnalyzer.RuleId);

        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            string title = MicrosoftCodeQualityAnalyzersResources.ImplementStandardExceptionConstructorsTitle;

            // One diagnostic is reported per missing constructor, all at the same location, so the fix has to
            // run for every one of them rather than only the first.
            RegisterCodeFix(context, title, title);
            return Task.CompletedTask;
        }

        protected override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SyntaxGenerator generator = editor.Generator;
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);
            SyntaxNode targetNode = generator.GetDeclaration(node, DeclarationKind.Class);

            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (model.GetDeclaredSymbol(targetNode, cancellationToken) is not INamedTypeSymbol typeSymbol)
            {
                return;
            }

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
                                                generator.ParameterDeclaration("message", generator.TypeExpression(model.Compilation.GetSpecialType(SpecialType.System_String)))
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
                                                generator.ParameterDeclaration("message", generator.TypeExpression(model.Compilation.GetSpecialType(SpecialType.System_String))),
                                                generator.ParameterDeclaration("innerException", generator.TypeExpression(model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemException)))
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
    }
}