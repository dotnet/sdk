// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.GenAPI
{
    internal static class SyntaxNodeExtensions
    {
        public static SyntaxNode Rewrite(this SyntaxNode node, CSharpSyntaxRewriter rewriter) => rewriter.Visit(node);

        public static SyntaxNode AddMemberAttributes(this SyntaxNode node,
            SyntaxGenerator syntaxGenerator,
            ISymbolFilter symbolFilter,
            ISymbol member)
        {
            foreach (AttributeData attribute in member.GetAttributes().ExcludeNonVisibleOutsideOfAssembly(symbolFilter))
            {
                // The C# compiler emits the DefaultMemberAttribute on any type containing an indexer.
                // In C# it is an error to manually attribute a type with the DefaultMemberAttribute if the type also declares an indexer.
                if (member is INamedTypeSymbol typeMember && typeMember.HasIndexer() && attribute.IsDefaultMemberAttribute())
                {
                    continue;
                }

                if (IsReservedAttribute(attribute.AttributeClass))
                {
                    continue;
                }

                node = syntaxGenerator.AddAttributes(node, syntaxGenerator.Attribute(attribute));
            }
            return node;
        }

        private static HashSet<string> _reservedTypes = new HashSet<string>(StringComparer.Ordinal)
            {
                "DynamicAttribute",
                "IsReadOnlyAttribute",
                "IsUnmanagedAttribute",
                "IsByRefLikeAttribute",
                "TupleElementNamesAttribute",
                "NullableAttribute",
                "NullableContextAttribute",
                "NullablePublicOnlyAttribute",
                "NativeIntegerAttribute",
                "ExtensionAttribute",
                "RequiredMemberAttribute",
                "ScopedRefAttribute",
                "RefSafetyRulesAttribute"
            };

        /// <summary>
        /// Determines if an attribute is a reserved attribute class -- these are attributes that may
        /// only be applied by the compiler and are an error to be applied by the user in source.
        /// See https://github.com/dotnet/roslyn/blob/b8f6dd56f1a0860fcd822bc1e70bec56dc1e97ea/src/Compilers/CSharp/Portable/Symbols/Symbol.cs#L1421
        /// </summary>
        /// <param name="attributeClass">The type of attribute</param>
        /// <returns>True if the attribute type is reserved.</returns>
        private static bool IsReservedAttribute(INamedTypeSymbol? attributeClass)
        {
            return attributeClass != null && _reservedTypes.Contains(attributeClass.Name) &&
                attributeClass.ContainingNamespace.ToDisplayString().Equals("System.Runtime.CompilerServices", StringComparison.Ordinal);
        }
    }
}
