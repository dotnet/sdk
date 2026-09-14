// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
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
        protected override async Task<Document> ImplementInterfacesOnDynamicCastableImplementationAsync(
            SyntaxNode root,
            SyntaxNode declaration,
            Document document,
            SyntaxGenerator generator,
            CancellationToken cancellationToken)
        {
            var model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var type = (INamedTypeSymbol)model.GetDeclaredSymbol(declaration, cancellationToken)!;

            var defaultMethodBodyStatements = generator.DefaultMethodBody(model.Compilation).ToArray();
            var generatedMembers = new List<SyntaxNode>();
            foreach (var iface in type.AllInterfaces)
            {
                foreach (var member in iface.GetMembers())
                {
                    if (member.IsAbstract && type.FindImplementationForInterfaceMember(member) is null)
                    {
                        var implementation = member.Kind switch
                        {
                            SymbolKind.Method => GenerateMethodImplementation((IMethodSymbol)member),
                            SymbolKind.Property => GeneratePropertyImplementation((IPropertySymbol)member),
                            SymbolKind.Event => GenerateEventImplementation((IEventSymbol)member, generator, defaultMethodBodyStatements),
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

            return document.WithSyntaxRoot(root.ReplaceNode(declaration, typeDeclaration));

            SyntaxNode? GenerateMethodImplementation(IMethodSymbol method)
            {
                // Method with associated symbols are not standalone methods that a user would write in C#.
                // They're methods like property or event accessors, which are covered in the following local functions.
                if (method.AssociatedSymbol is not null)
                {
                    return null;
                }

                var methodDeclaration = generator.MethodDeclaration(method);
                methodDeclaration = generator.WithModifiers(methodDeclaration, generator.GetModifiers(methodDeclaration).WithIsAbstract(false));
                return generator.WithStatements(methodDeclaration, defaultMethodBodyStatements);
            }

            SyntaxNode GeneratePropertyImplementation(IPropertySymbol property)
            {
                var propertyDeclaration = property.IsIndexer
                    ? generator.IndexerDeclaration(property)
                    : generator.PropertyDeclaration(property);
                propertyDeclaration = generator.WithModifiers(propertyDeclaration, generator.GetModifiers(propertyDeclaration).WithIsAbstract(false));

                // Remove default property accessors.
                propertyDeclaration = generator.WithAccessorDeclarations(propertyDeclaration);

                if (property.GetMethod is not null
                    && model.Compilation.IsSymbolAccessibleWithin(property.GetMethod, type))
                {
                    propertyDeclaration = generator.WithGetAccessorStatements(propertyDeclaration, defaultMethodBodyStatements);
                }

                if (property.SetMethod is not null
                    && model.Compilation.IsSymbolAccessibleWithin(property.SetMethod, type))
                {
                    propertyDeclaration = AddSetAccessor(property, propertyDeclaration, generator, defaultMethodBodyStatements, includeAccessibility: false);
                }

                return propertyDeclaration;
            }
        }

        private static SyntaxNode AddSetAccessor(
            IPropertySymbol property,
            SyntaxNode declaration,
            SyntaxGenerator generator,
            SyntaxNode[] defaultMethodBodyStatements,
            bool includeAccessibility)
        {
            if (!property.SetMethod!.IsInitOnly)
            {
                return generator.WithSetAccessorStatements(declaration, defaultMethodBodyStatements);
            }

            var setAccessorAccessibility = includeAccessibility && property.DeclaredAccessibility != property.SetMethod!.DeclaredAccessibility
                ? property.SetMethod.DeclaredAccessibility
                : Accessibility.NotApplicable;

            var setAccessor = (AccessorDeclarationSyntax)generator.SetAccessorDeclaration(setAccessorAccessibility, defaultMethodBodyStatements);

            var propertyDeclaration = (PropertyDeclarationSyntax)declaration;

            SyntaxNode? oldInitAccessor = null;

            foreach (var accessor in propertyDeclaration.AccessorList!.Accessors)
            {
                if (accessor.IsKind(SyntaxKind.InitAccessorDeclaration))
                {
                    oldInitAccessor = accessor;
                    break;
                }
            }

            if (oldInitAccessor is not null)
            {
                propertyDeclaration = propertyDeclaration.WithAccessorList(propertyDeclaration.AccessorList.RemoveNode(oldInitAccessor, SyntaxRemoveOptions.KeepNoTrivia));
            }

            return propertyDeclaration.WithAccessorList(propertyDeclaration.AccessorList!.AddAccessors(
                SyntaxFactory.AccessorDeclaration(
                        SyntaxKind.InitAccessorDeclaration,
                        setAccessor.AttributeLists,
                        setAccessor.Modifiers,
                        SyntaxFactory.Token(SyntaxKind.InitKeyword),
                        setAccessor.Body,
                        setAccessor.ExpressionBody,
                        setAccessor.SemicolonToken)));
        }

        private static SyntaxNode GenerateEventImplementation(
            IEventSymbol evt,
            SyntaxGenerator generator,
            SyntaxNode[] defaultMethodBodyStatements)
        {
            var eventDeclaration = generator.CustomEventDeclaration(evt);
            eventDeclaration = generator.WithModifiers(eventDeclaration, generator.GetModifiers(eventDeclaration).WithIsAbstract(false));

            // Explicitly use the C# syntax APIs to work around https://github.com/dotnet/roslyn/issues/53649
            return ((EventDeclarationSyntax)eventDeclaration).WithAccessorList(
                SyntaxFactory.AccessorList(
                    SyntaxFactory.List(
                new[]
                {
                        (AccessorDeclarationSyntax)generator.WithStatements(generator.GetAccessor(eventDeclaration, DeclarationKind.AddAccessor), defaultMethodBodyStatements),
                        (AccessorDeclarationSyntax)generator.WithStatements(generator.GetAccessor(eventDeclaration, DeclarationKind.RemoveAccessor), defaultMethodBodyStatements),
                })));
        }

        protected override async Task<Document> MakeMemberDeclaredOnImplementationTypeStaticAsync(SyntaxNode declaration, Document document, CancellationToken cancellationToken)
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
                            SyntaxFactory.SingletonSeparatedList((ArgumentSyntax)generator.Argument(invocation.target))
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
