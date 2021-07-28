// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Lightup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.NetCore.Analyzers.InteropServices;

namespace Microsoft.NetCore.CSharp.Analyzers.InteropServices
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpDynamicInterfaceCastableImplementationFixer : DynamicInterfaceCastableImplementationFixer
    {
        // Manually define the InitKeyword and InitAccessorDeclaration values since we compile against too old of a Roslyn to use it directly.
        // We only generate init accessors if they already exist, so we don't need to worry about these being unrecognized.
        private const SyntaxKind InitKeyword = (SyntaxKind)8443;
        private const SyntaxKind InitAccessorDeclaration = (SyntaxKind)9060;

        protected override async Task<Document> ImplementInterfacesOnDynamicCastableImplementation(
            SyntaxNode declaration,
            Document document,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            var type = (INamedTypeSymbol)editor.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken);
            var generator = editor.Generator;

            var defaultMethodBodyStatements = generator.DefaultMethodBody(editor.SemanticModel.Compilation).ToArray();
            var generatedMembers = new List<SyntaxNode>();
            foreach (var iface in type.AllInterfaces)
            {
                foreach (var member in iface.GetMembers())
                {
                    if (!member.IsStatic && type.FindImplementationForInterfaceMember(member) is null)
                    {
                        var implementation = member.Kind switch
                        {
                            SymbolKind.Method => GenerateMethodImplementation((IMethodSymbol)member),
                            SymbolKind.Property => GeneratePropertyImplementation((IPropertySymbol)member),
                            SymbolKind.Event => GenerateEventImplementation((IEventSymbol)member, declaration, generator, defaultMethodBodyStatements),
                            _ => null
                        };
                        if (implementation is not null)
                        {
                            generatedMembers.Add(generator.AsPrivateInterfaceImplementation(
                                implementation,
                                generator.NameExpression(member.ContainingType)));
                        }
                    }
                }
            }

            // Explicitly use the C# syntax APIs to work around https://github.com/dotnet/roslyn/issues/53605
            var typeDeclaration = (TypeDeclarationSyntax)declaration;
            typeDeclaration = typeDeclaration.AddMembers(generatedMembers.Cast<MemberDeclarationSyntax>().ToArray());

            editor.ReplaceNode(declaration, typeDeclaration);

            return editor.GetChangedDocument();

            SyntaxNode? GenerateMethodImplementation(IMethodSymbol method)
            {
                // Method with associated symbols are not standalone methods that a user would write in C#.
                // They're methods like property or event accessors, which are covered in the following local functions.
                if (method.AssociatedSymbol is not null)
                {
                    return null;
                }
                var methodDecl = generator.MethodDeclaration(method);
                methodDecl = generator.WithModifiers(methodDecl, generator.GetModifiers(declaration).WithIsAbstract(false));
                return generator.WithStatements(methodDecl, defaultMethodBodyStatements);
            }

            SyntaxNode GeneratePropertyImplementation(IPropertySymbol property)
            {
                var propertyDecl = property.IsIndexer
                    ? generator.IndexerDeclaration(property)
                    : generator.PropertyDeclaration(property);
                propertyDecl = generator.WithModifiers(propertyDecl, generator.GetModifiers(declaration).WithIsAbstract(false));

                // Remove default property accessors.
                propertyDecl = generator.WithAccessorDeclarations(propertyDecl);

                if (property.GetMethod is not null
                    && editor.SemanticModel.Compilation.IsSymbolAccessibleWithin(property.GetMethod, type))
                {
                    propertyDecl = generator.WithGetAccessorStatements(propertyDecl, defaultMethodBodyStatements);
                }
                if (property.SetMethod is not null
                    && editor.SemanticModel.Compilation.IsSymbolAccessibleWithin(property.SetMethod, type))
                {
                    propertyDecl = AddSetAccessor(property, propertyDecl, generator, defaultMethodBodyStatements, includeAccessibility: false);
                }

                return propertyDecl;
            }
        }

        private static SyntaxNode AddSetAccessor(
            IPropertySymbol property,
            SyntaxNode declaration,
            SyntaxGenerator generator,
            SyntaxNode[] defaultMethodBodyStatements,
            bool includeAccessibility)
        {
            if (!property.SetMethod.IsInitOnly())
            {
                return generator.WithSetAccessorStatements(declaration, defaultMethodBodyStatements);
            }

            var setAccessorAccessibility = includeAccessibility && property.DeclaredAccessibility != property.SetMethod.DeclaredAccessibility
                ? property.SetMethod.DeclaredAccessibility
                : Accessibility.NotApplicable;

            var setAccessor = (AccessorDeclarationSyntax)generator.SetAccessorDeclaration(setAccessorAccessibility, defaultMethodBodyStatements);

            var propDecl = (PropertyDeclarationSyntax)declaration;

            SyntaxNode? oldInitAccessor = null;

            foreach (var accessor in propDecl.AccessorList.Accessors)
            {
                if (accessor.IsKind(InitAccessorDeclaration))
                {
                    oldInitAccessor = accessor;
                    break;
                }
            }

            if (oldInitAccessor is not null)
            {
                propDecl = propDecl.WithAccessorList(propDecl.AccessorList.RemoveNode(oldInitAccessor, SyntaxRemoveOptions.KeepNoTrivia));
            }

            return propDecl.WithAccessorList(propDecl.AccessorList.AddAccessors(
                SyntaxFactory.AccessorDeclaration(
                        InitAccessorDeclaration,
                        setAccessor.AttributeLists,
                        setAccessor.Modifiers,
                        SyntaxFactory.Token(InitKeyword),
                        setAccessor.Body,
                        setAccessor.ExpressionBody,
                        setAccessor.SemicolonToken)));
        }

        private static SyntaxNode GenerateEventImplementation(
            IEventSymbol evt,
            SyntaxNode declaration,
            SyntaxGenerator generator,
            SyntaxNode[] defaultMethodBodyStatements)
        {
            var eventDecl = generator.CustomEventDeclaration(evt);
            eventDecl = generator.WithModifiers(eventDecl, generator.GetModifiers(declaration).WithIsAbstract(false));

            // Explicitly use the C# syntax APIs to work around https://github.com/dotnet/roslyn/issues/53649
            var eventDeclaration = (EventDeclarationSyntax)eventDecl;

            return eventDeclaration.WithAccessorList(
                SyntaxFactory.AccessorList(
                    SyntaxFactory.List(
                new[]
                {
                        generator.WithStatements(generator.GetAccessor(eventDecl, DeclarationKind.AddAccessor), defaultMethodBodyStatements),
                        generator.WithStatements(generator.GetAccessor(eventDecl, DeclarationKind.RemoveAccessor), defaultMethodBodyStatements),
                })));
        }

        protected override async Task<Document> MakeMemberDeclaredOnImplementationTypeStatic(SyntaxNode declaration, Document document, CancellationToken cancellationToken)
        {
            var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var generator = editor.Generator;
            var defaultMethodBodyStatements = generator.DefaultMethodBody(editor.SemanticModel.Compilation).ToArray();

            var symbol = editor.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken);

            if (symbol is not IMethodSymbol)
            {
                // We can't automatically make properties or events static.
                return document;
            }

            // We're going to convert the this parameter to a @this parameter at the start of the parameter list,
            // so we need to warn if the symbol already exists in scope since the fix may produce broken code.

            SymbolInfo introducedThisParamInfo = editor.SemanticModel.GetSpeculativeSymbolInfo(
                declaration.SpanStart,
                SyntaxFactory.IdentifierName(EscapedThisToken),
                SpeculativeBindingOption.BindAsExpression);

            bool shouldWarn = false;
            if (introducedThisParamInfo.Symbol is not null)
            {
                shouldWarn = true;
            }

            var referencedSymbols = await SymbolFinder.FindReferencesAsync(
                symbol, document.Project.Solution, cancellationToken).ConfigureAwait(false);

            List<(InvocationExpressionSyntax invocation, ExpressionSyntax target)> invocations = new();

            foreach (var referencedSymbol in referencedSymbols)
            {
                foreach (var location in referencedSymbol.Locations)
                {
                    if (!location.Document.Id.Equals(document.Id))
                    {
                        shouldWarn = true;
                        continue;
                    }
                    // We limited the search scope to the single document, 
                    // so all reference should be in the same tree.
                    var referenceNode = root.FindNode(location.Location.SourceSpan);
                    if (referenceNode is not IdentifierNameSyntax identifierNode)
                    {
                        // Unexpected scenario, skip and warn.
                        shouldWarn = true;
                        continue;
                    }

                    if (identifierNode.Parent is InvocationExpressionSyntax invocationOnThis)
                    {
                        invocations.Add((invocationOnThis, SyntaxFactory.ThisExpression()));
                    }
                    else if (identifierNode.Parent is MemberAccessExpressionSyntax methodAccess
                        && methodAccess.Parent is InvocationExpressionSyntax invocation)
                    {
                        invocations.Add((invocation, methodAccess.Expression));
                    }
                    else
                    {
                        // We won't be able to fix non-invocation references, 
                        // e.g. creating a delegate. 
                        shouldWarn = true;
                    }
                }
            }

            // Fix all invocations by passing in this argument.
            foreach (var invocation in invocations)
            {
                editor.ReplaceNode(
                    invocation.invocation,
                    (node, generator) =>
                    {
                        var currentInvocation = (InvocationExpressionSyntax)node;

                        var newArgList = currentInvocation.ArgumentList.WithArguments(
                            SyntaxFactory.SingletonSeparatedList(generator.Argument(invocation.target))
                                .AddRange(currentInvocation.ArgumentList.Arguments));
                        return currentInvocation.WithArgumentList(newArgList).WithExpression(SyntaxFactory.IdentifierName(symbol.Name));
                    });
            }

            editor.ReplaceNode(
                declaration,
                (node, generator) =>
                {
                    var updatedMethod = generator.WithModifiers(node, DeclarationModifiers.From(symbol)
                        .WithIsAbstract(false)
                        .WithIsVirtual(false)
                        .WithIsSealed(false)
                        .WithIsStatic(true));

                    if (symbol.IsAbstract)
                    {
                        updatedMethod = generator.WithStatements(updatedMethod, defaultMethodBodyStatements);
                    }

                    updatedMethod = ((MethodDeclarationSyntax)updatedMethod).WithParameterList(
                        SyntaxFactory.ParameterList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Parameter(EscapedThisToken)
                                .WithType(SyntaxFactory.ParseTypeName(symbol.ContainingType.Name)))
                            .AddRange(((MethodDeclarationSyntax)updatedMethod).ParameterList.Parameters)));

                    ThisToIntroducedParameterRewriter rewriter = new ThisToIntroducedParameterRewriter();
                    updatedMethod = rewriter.Visit(updatedMethod);

                    if (shouldWarn)
                    {
                        updatedMethod = updatedMethod.WithAdditionalAnnotations(CreatePossibleInvalidCodeWarning());
                    }

                    return updatedMethod;
                });

            return editor.GetChangedDocument();
        }

        private static readonly SyntaxToken EscapedThisToken = SyntaxFactory.Identifier(
                    SyntaxFactory.TriviaList(),
                    SyntaxKind.IdentifierToken,
                    "@this",
                    "this",
                    SyntaxFactory.TriviaList());

        protected override bool CodeFixSupportsDeclaration(SyntaxNode declaration)
        {
            return !declaration.IsKind(SyntaxKind.VariableDeclarator);
        }

        private class ThisToIntroducedParameterRewriter : CSharpSyntaxRewriter
        {
            public override SyntaxNode VisitThisExpression(ThisExpressionSyntax node)
            {
                return SyntaxFactory.IdentifierName(EscapedThisToken);
            }
        }
    }
}
