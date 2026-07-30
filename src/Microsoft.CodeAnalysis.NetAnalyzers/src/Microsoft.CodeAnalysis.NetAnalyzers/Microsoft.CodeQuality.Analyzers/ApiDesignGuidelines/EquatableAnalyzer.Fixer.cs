// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Analyzer.Utilities;
using System.Threading;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1066: Implement IEquatable when overriding Object.Equals
    /// CA1067: Override Object.Equals(object) when implementing IEquatable{T}
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class EquatableFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(EquatableAnalyzer.ImplementIEquatableRuleId, EquatableAnalyzer.OverrideObjectEqualsRuleId);

        // The two actions generate different members, so the fix-all pass has to be told which one the user
        // picked - DocumentBasedFixAllProvider hands over every diagnostic it collected without filtering by
        // the equivalence key.
        public override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(
                static fixAllContext => fixAllContext.CodeActionEquivalenceKey,
                ApplyFixAsync);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(context.Document);
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode declaration = root.FindNode(context.Span);
            declaration = generator.GetDeclaration(declaration);
            if (declaration == null)
            {
                return;
            }

            SemanticModel model =
                await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (model.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol type || type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
            {
                return;
            }

            INamedTypeSymbol? equatableType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIEquatable1);
            if (equatableType == null)
            {
                return;
            }

            Document document = context.Document;
            ImmutableArray<Diagnostic> diagnostics = context.Diagnostics;

            if (type.TypeKind == TypeKind.Struct && !TypeImplementsEquatable(type, equatableType))
            {
                string title = MicrosoftCodeQualityAnalyzersResources.ImplementEquatable;
                context.RegisterCodeFix(CodeAction.Create(
                    title,
                    cancellationToken => SyntaxEditorFixAllProvider.ApplyFixesAsync(
                        document,
                        diagnostics,
                        (doc, diagnostic, editor, token) => ApplyFixAsync(doc, diagnostic, editor, title, token),
                        cancellationToken),
                    equivalenceKey: title), diagnostics);
            }

            if (!type.OverridesEquals())
            {
                string title = MicrosoftCodeQualityAnalyzersResources.OverrideEqualsOnImplementingIEquatableCodeActionTitle;
                context.RegisterCodeFix(CodeAction.Create(
                    title,
                    cancellationToken => SyntaxEditorFixAllProvider.ApplyFixesAsync(
                        document,
                        diagnostics,
                        (doc, diagnostic, editor, token) => ApplyFixAsync(doc, diagnostic, editor, title, token),
                        cancellationToken),
                    equivalenceKey: title), diagnostics);
            }
        }

        private static async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor,
            string? equivalenceKey, CancellationToken cancellationToken)
        {
            SyntaxNode? declaration = editor.Generator.GetDeclaration(editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan));
            if (declaration == null)
            {
                return;
            }

            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (model.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol type ||
                type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
            {
                return;
            }

            INamedTypeSymbol? equatableType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIEquatable1);
            if (equatableType == null)
            {
                return;
            }

            if (equivalenceKey == MicrosoftCodeQualityAnalyzersResources.ImplementEquatable)
            {
                if (type.TypeKind == TypeKind.Struct && !TypeImplementsEquatable(type, equatableType))
                {
                    ImplementEquatableInStruct(declaration, type, model.Compilation, equatableType, editor);
                }
            }
            else if (equivalenceKey == MicrosoftCodeQualityAnalyzersResources.OverrideEqualsOnImplementingIEquatableCodeActionTitle)
            {
                if (!type.OverridesEquals())
                {
                    OverrideObjectEquals(declaration, type, equatableType, editor);
                }
            }
        }

        private static bool TypeImplementsEquatable(INamedTypeSymbol type, INamedTypeSymbol equatableType)
        {
            INamedTypeSymbol constructedEquatable = equatableType.Construct(type);
            INamedTypeSymbol? implementation = type
                .Interfaces
                .FirstOrDefault(x => x.Equals(constructedEquatable));
            return implementation != null;
        }

        private static void ImplementEquatableInStruct(SyntaxNode declaration,
            INamedTypeSymbol typeSymbol, Compilation compilation, INamedTypeSymbol equatableType,
            SyntaxEditor editor)
        {
            var generator = editor.Generator;

            var equalsMethod = generator.MethodDeclaration(
                WellKnownMemberNames.ObjectEquals,
                new[]
                {
                    generator.ParameterDeclaration("other", generator.TypeExpression(typeSymbol))
                },
                returnType: generator.TypeExpression(SpecialType.System_Boolean),
                accessibility: Accessibility.Public,
                statements: generator.DefaultMethodBody(compilation));

            editor.AddMember(declaration, equalsMethod);

            INamedTypeSymbol constructedType = equatableType.Construct(typeSymbol);
            editor.AddInterfaceType(declaration, generator.TypeExpression(constructedType));
        }

        private static void OverrideObjectEquals(SyntaxNode declaration,
            INamedTypeSymbol typeSymbol, INamedTypeSymbol equatableType, SyntaxEditor editor)
        {
            var generator = editor.Generator;

            var argumentName = generator.IdentifierName("obj");

            SyntaxNode returnStatement;

            if (HasExplicitEqualsImplementation(typeSymbol, equatableType))
            {
                returnStatement = typeSymbol.TypeKind == TypeKind.Class
                    ? GetReturnStatementForExplicitClass(generator, typeSymbol, argumentName, equatableType)
                    : GetReturnStatementForExplicitStruct(generator, typeSymbol, argumentName, equatableType);
            }
            else
            {
                returnStatement = typeSymbol.TypeKind == TypeKind.Class
                    ? GetReturnStatementForImplicitClass(generator, typeSymbol, argumentName)
                    : GetReturnStatementForImplicitStruct(generator, typeSymbol, argumentName);
            }

            var equalsMethod = generator.MethodDeclaration(
                WellKnownMemberNames.ObjectEquals,
                new[]
                {
                    generator.ParameterDeclaration(argumentName.ToString(),
                        generator.TypeExpression(SpecialType.System_Object))
                },
                returnType: generator.TypeExpression(SpecialType.System_Boolean),
                accessibility: Accessibility.Public,
                modifiers: DeclarationModifiers.Override,
                statements: new[] { returnStatement });

            editor.AddMember(declaration, equalsMethod);
        }

        private static bool HasExplicitEqualsImplementation(INamedTypeSymbol typeSymbol, INamedTypeSymbol equatableType)
        {
            INamedTypeSymbol constructedType = equatableType.Construct(typeSymbol);
            IMethodSymbol constructedEqualsMethod = constructedType.GetMembers().OfType<IMethodSymbol>().FirstOrDefault();

            foreach (IMethodSymbol method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
            {
                foreach (IMethodSymbol explicitImplementation in method.ExplicitInterfaceImplementations)
                {
                    if (explicitImplementation.Equals(constructedEqualsMethod))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static SyntaxNode GetReturnStatementForExplicitClass(SyntaxGenerator generator,
            INamedTypeSymbol typeSymbol, SyntaxNode argumentName, INamedTypeSymbol equatableType)
        {
            return generator.ReturnStatement(
                generator.InvocationExpression(
                    generator.MemberAccessExpression(
                        generator.CastExpression(
                            equatableType.Construct(typeSymbol),
                            generator.ThisExpression()),
                        WellKnownMemberNames.ObjectEquals),
                    generator.TryCastExpression(
                        argumentName,
                        typeSymbol)));
        }

        private static SyntaxNode GetReturnStatementForExplicitStruct(SyntaxGenerator generator,
            INamedTypeSymbol typeSymbol, SyntaxNode argumentName, INamedTypeSymbol equatableType)
        {
            return generator.ReturnStatement(
                generator.LogicalAndExpression(
                    generator.IsTypeExpression(
                        argumentName,
                        typeSymbol),
                    generator.InvocationExpression(
                        generator.MemberAccessExpression(
                            generator.CastExpression(
                                equatableType.Construct(typeSymbol),
                                generator.ThisExpression()),
                            WellKnownMemberNames.ObjectEquals),
                        generator.CastExpression(
                            typeSymbol,
                            argumentName))));
        }

        private static SyntaxNode GetReturnStatementForImplicitClass(SyntaxGenerator generator,
            INamedTypeSymbol typeSymbol, SyntaxNode argumentName)
        {
            return generator.ReturnStatement(
                generator.InvocationExpression(
                    generator.IdentifierName(WellKnownMemberNames.ObjectEquals),
                    generator.Argument(
                        generator.TryCastExpression(
                            argumentName,
                            typeSymbol))));
        }

        private static SyntaxNode GetReturnStatementForImplicitStruct(SyntaxGenerator generator,
            INamedTypeSymbol typeSymbol, SyntaxNode argumentName)
        {
            return generator.ReturnStatement(
                generator.LogicalAndExpression(
                    generator.IsTypeExpression(
                        argumentName,
                        typeSymbol),
                    generator.InvocationExpression(
                        generator.IdentifierName(WellKnownMemberNames.ObjectEquals),
                        generator.CastExpression(
                            typeSymbol,
                            argumentName))));
        }
    }
}