// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities.Lightup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using NullableAnnotation = Analyzer.Utilities.Lightup.NullableAnnotation;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    public sealed partial class CSharpForwardCancellationTokenToInvocationsFixer
    {
        private class TypeNameVisitor : SymbolVisitor<TypeSyntax>
        {
            public static TypeSyntax GetTypeSyntaxForSymbol(INamespaceOrTypeSymbol symbol)
            {
                return symbol.Accept(new TypeNameVisitor()).WithAdditionalAnnotations(Simplifier.Annotation);
            }

            public override TypeSyntax DefaultVisit(ISymbol symbol)
                => throw new NotImplementedException();

            public override TypeSyntax VisitAlias(IAliasSymbol symbol)
            {
                return AddInformationTo(ToIdentifierName(symbol.Name));
            }

            public override TypeSyntax VisitDynamicType(IDynamicTypeSymbol symbol)
            {
                return AddInformationTo(IdentifierName("dynamic"));
            }

            public override TypeSyntax VisitNamedType(INamedTypeSymbol symbol)
            {
                if (TryCreateNativeIntegerType(symbol, out var typeSyntax))
                    return typeSyntax;

                typeSyntax = CreateSimpleTypeSyntax(symbol);
                if (!(typeSyntax is SimpleNameSyntax))
                    return typeSyntax;

                var simpleNameSyntax = (SimpleNameSyntax)typeSyntax;
                if (symbol.ContainingType is not null)
                {
                    if (symbol.ContainingType.TypeKind != TypeKind.Submission)
                    {
                        var containingTypeSyntax = symbol.ContainingType.Accept(this);
                        if (containingTypeSyntax is NameSyntax name)
                        {
                            typeSyntax = AddInformationTo(
                                QualifiedName(name, simpleNameSyntax));
                        }
                        else
                        {
                            typeSyntax = AddInformationTo(simpleNameSyntax);
                        }
                    }
                }
                else if (symbol.ContainingNamespace is not null)
                {
                    if (symbol.ContainingNamespace.IsGlobalNamespace)
                    {
                        if (symbol.TypeKind != TypeKind.Error)
                        {
                            typeSyntax = AddGlobalAlias(simpleNameSyntax);
                        }
                    }
                    else
                    {
                        var container = symbol.ContainingNamespace.Accept(this)!;
                        typeSyntax = AddInformationTo(QualifiedName(
                            (NameSyntax)container,
                            simpleNameSyntax));
                    }
                }

                if (symbol.NullableAnnotation() == NullableAnnotation.Annotated &&
                    !symbol.IsValueType)
                {
                    typeSyntax = AddInformationTo(NullableType(typeSyntax));
                }

                return typeSyntax;
            }

            public override TypeSyntax VisitNamespace(INamespaceSymbol symbol)
            {
                var syntax = AddInformationTo(ToIdentifierName(symbol.Name));
                if (symbol.ContainingNamespace == null)
                {
                    return syntax;
                }

                if (symbol.ContainingNamespace.IsGlobalNamespace)
                {
                    return AddGlobalAlias(syntax);
                }
                else
                {
                    var container = symbol.ContainingNamespace.Accept(this)!;
                    return AddInformationTo(QualifiedName(
                        (NameSyntax)container,
                        syntax));
                }
            }

            public override TypeSyntax VisitTypeParameter(ITypeParameterSymbol symbol)
            {
                TypeSyntax typeSyntax = AddInformationTo(ToIdentifierName(symbol.Name));
                if (symbol.NullableAnnotation() == NullableAnnotation.Annotated)
                    typeSyntax = AddInformationTo(NullableType(typeSyntax));

                return typeSyntax;
            }

            private TypeSyntax CreateSimpleTypeSyntax(INamedTypeSymbol symbol)
            {
                if (symbol.IsTupleType && symbol.TupleUnderlyingType != null && !symbol.Equals(symbol.TupleUnderlyingType))
                {
                    return CreateSimpleTypeSyntax(symbol.TupleUnderlyingType);
                }

                if (string.IsNullOrEmpty(symbol.Name) || symbol.IsAnonymousType)
                {
                    return CreateSystemObject();
                }

                if (symbol.TypeParameters.Length == 0)
                {
                    if (symbol.TypeKind == TypeKind.Error && symbol.Name == "var")
                    {
                        return CreateSystemObject();
                    }

                    return ToIdentifierName(symbol.Name);
                }

                var typeArguments = symbol.IsUnboundGenericType
                    ? Enumerable.Repeat((TypeSyntax)OmittedTypeArgument(), symbol.TypeArguments.Length)
                    : symbol.TypeArguments.Select(t => GetTypeSyntaxForSymbol(t));

                return GenericName(
                    ToIdentifierToken(symbol.Name),
                    TypeArgumentList(SeparatedList(typeArguments)));
            }

            private static QualifiedNameSyntax CreateSystemObject()
            {
                return QualifiedName(
                    AliasQualifiedName(
                        CreateGlobalIdentifier(),
                        IdentifierName("System")),
                    IdentifierName("Object"));
            }

            private static TTypeSyntax AddInformationTo<TTypeSyntax>(TTypeSyntax syntax)
                where TTypeSyntax : TypeSyntax
            {
                syntax = syntax.WithLeadingTrivia(ElasticMarker).WithTrailingTrivia(ElasticMarker);
                return syntax;
            }

            /// <summary>
            /// We always unilaterally add "global::" to all named types/namespaces.  This
            /// will then be trimmed off if possible by the simplifier
            /// </summary>
            private static TypeSyntax AddGlobalAlias(SimpleNameSyntax syntax)
            {
                return AddInformationTo(AliasQualifiedName(CreateGlobalIdentifier(), syntax));
            }

            private static IdentifierNameSyntax ToIdentifierName(string identifier)
                => IdentifierName(ToIdentifierToken(identifier));

            private static IdentifierNameSyntax CreateGlobalIdentifier()
                => IdentifierName(Token(SyntaxKind.GlobalKeyword));

            private static bool TryCreateNativeIntegerType(INamedTypeSymbol symbol, [NotNullWhen(true)] out TypeSyntax? syntax)
            {
                if (symbol.IsNativeIntegerType())
                {
                    syntax = IdentifierName(symbol.SpecialType == SpecialType.System_IntPtr ? "nint" : "nuint");
                    return true;
                }

                syntax = null;
                return false;
            }

            private static SyntaxToken ToIdentifierToken(string identifier)
            {
                var escaped = EscapeIdentifier(identifier);

                if (escaped.Length == 0 || escaped[0] != '@')
                {
                    return Identifier(escaped);
                }

                var unescaped = identifier.StartsWith("@", StringComparison.Ordinal)
                    ? identifier[1..]
                    : identifier;

                var token = Identifier(
                    default, SyntaxKind.None, "@" + unescaped, unescaped, default);

                if (!identifier.StartsWith("@", StringComparison.Ordinal))
                {
                    token = token.WithAdditionalAnnotations(Simplifier.Annotation);
                }

                return token;
            }

            private static string EscapeIdentifier(string identifier)
            {
                var nullIndex = identifier.IndexOf('\0');
                if (nullIndex >= 0)
                {
                    identifier = identifier.Substring(0, nullIndex);
                }

                var needsEscaping = SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None;

                return needsEscaping ? "@" + identifier : identifier;
            }
        }
    }
}
