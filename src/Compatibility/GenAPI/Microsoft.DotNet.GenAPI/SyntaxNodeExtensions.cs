// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.GenAPI
{
    internal static class SyntaxNodeExtensions
    {
        // The default metadata name for an indexer that doesn't customize its name with the IndexerNameAttribute.
        private const string DefaultIndexerName = "Item";

        public static SyntaxNode Rewrite(this SyntaxNode node, CSharpSyntaxRewriter rewriter) => rewriter.Visit(node);

        public static SyntaxNode AddMemberAttributes(this SyntaxNode node,
            SyntaxGenerator syntaxGenerator,
            ISymbol member,
            ISymbolFilter attributeDataSymbolFilter)
        {
            // The IndexerNameAttribute is consumed by the compiler to change the metadata name of an indexer and
            // is therefore not part of the symbol's custom attributes. SyntaxGenerator doesn't emit it either
            // (see https://github.com/dotnet/roslyn/issues/72007), so synthesize it here for indexers that use a
            // non-default name. Explicit interface implementations inherit their name from the interface and can't
            // carry the attribute, so they are skipped.
            if (member is IPropertySymbol property &&
                property.IsIndexer &&
                property.ExplicitInterfaceImplementations.IsEmpty &&
                !string.Equals(property.MetadataName, DefaultIndexerName, StringComparison.Ordinal))
            {
                node = syntaxGenerator.AddAttributes(node, syntaxGenerator.Attribute(typeof(IndexerNameAttribute).FullName!,
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(property.MetadataName)))));
            }

            foreach (AttributeData attribute in member.GetAttributes().ExcludeNonVisibleOutsideOfAssembly(attributeDataSymbolFilter))
            {
                // The C# compiler emits the DefaultMemberAttribute on any type containing an indexer.
                // In C# it is an error to manually attribute a type with the DefaultMemberAttribute if the type also declares an indexer.
                if (member is INamedTypeSymbol typeMember && typeMember.HasIndexer() && attribute.IsDefaultMemberAttribute())
                {
                    continue;
                }

                if (attribute.IsReserved())
                {
                    continue;
                }

                node = syntaxGenerator.AddAttributes(node, syntaxGenerator.Attribute(attribute));
            }

            return node;
        }
    }
}
