// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
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

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1066: Implement IEquatable when overriding Object.Equals
    /// CA1067: Override Object.Equals(object) when implementing IEquatable{T}
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class EquatableFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(EquatableAnalyzer.ImplementIEquatableRuleId, EquatableAnalyzer.OverrideObjectEqualsRuleId);

        public override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(context.Document);
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode declaration = root.FindNode(context.Span);
            declaration = generator.GetDeclaration(declaration);
            if (declaration == null)
            {
                return;
            }

            SemanticModel model =
                await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (model.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol type || type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
            {
                return;
            }

            INamedTypeSymbol? equatableType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIEquatable1);
            if (equatableType == null)
            {
                return;
            }

            if (type.TypeKind == TypeKind.Struct && !TypeImplementsEquatable(type, equatableType))
            {
                string title = MicrosoftCodeQualityAnalyzersResources.ImplementEquatable;
                context.RegisterCodeFix(new MyCodeAction(
                    title,
                    async ct =>
                        await ImplementEquatableInStructAsync(context.Document, declaration, type, model.Compilation,
                            equatableType, ct).ConfigureAwait(false),
                    equivalenceKey: title), context.Diagnostics);
            }

            if (!type.OverridesEquals())
            {
                string title = MicrosoftCodeQualityAnalyzersResources.OverrideEqualsOnImplementingIEquatableCodeActionTitle;
                context.RegisterCodeFix(new MyCodeAction(
                    title,
                    async ct =>
                        await OverrideObjectEqualsAsync(context.Document, declaration, type, equatableType,
                            ct).ConfigureAwait(false),
                    equivalenceKey: title), context.Diagnostics);
            }
        }

        private static bool TypeImplementsEquatable(INamedTypeSymbol type, INamedTypeSymbol equatableType)
        {
            INamedTypeSymbol constructedEquatable = equatableType.Construct(type);
            INamedTypeSymbol implementation = type
                .Interfaces
                .FirstOrDefault(x => x.Equals(constructedEquatable));
            return implementation != null;
        }

        private static async Task<Document> ImplementEquatableInStructAsync(Document document, SyntaxNode declaration,
            INamedTypeSymbol typeSymbol, Compilation compilation, INamedTypeSymbol equatableType,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
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

            return editor.GetChangedDocument();
        }

        private static async Task<Document> OverrideObjectEqualsAsync(Document document, SyntaxNode declaration,
            INamedTypeSymbol typeSymbol, INamedTypeSymbol equatableType, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
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

            return editor.GetChangedDocument();
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