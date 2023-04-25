// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.GenAPI.SyntaxRewriter
{
    /// <summary>
    /// Handles type forward assembly attributes and removes generic type arguments:
    ///     [assembly:TypeForwardedToAttribute(typeof(System.Collections.Generic.IAsyncEnumerable<A, B, C>))] ->
    ///     [assembly:TypeForwardedToAttribute(typeof(System.Collections.Generic.IAsyncEnumerable<,,>))]
    ///
    /// Also handles type forwards to Tuples
    ///     [assembly:TypeForwardedToAttribute(typeof((T1, T2, T3))] ->
    ///     [assembly:TypeForwardedToAttribute(typeof(System.ValueTuple<,,>))]
    /// </summary>
    public class TypeForwardAttributeCSharpSyntaxRewriter : CSharpSyntaxRewriter
    {
        private bool _inTypeForwardAttribute = false;
        private bool _inTypeOfExpression = false;

        /// <summary>
        /// Records if we're inside a `TypeForwardedTo` attribute.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public override SyntaxNode? VisitAttribute(AttributeSyntax node)
        {
            try
            {
                _inTypeForwardAttribute = IsTypeForwardAttribute(node);
                return base.VisitAttribute(node);
            }
            finally
            {
                _inTypeForwardAttribute = false;
            }

        }

        /// <summary>
        /// Records if we're inside a `typeof` expression.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public override SyntaxNode? VisitTypeOfExpression(TypeOfExpressionSyntax node)
        {
            try
            {
                _inTypeOfExpression = true;
                return base.VisitTypeOfExpression(node);
            }
            finally
            {
                _inTypeOfExpression = false;
            }
        }

        /// <summary>
        /// Replace Type<TA, TB, TC> with Type<,,> when it occurs in typeof() expression in TypeForwardAttribute.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public override SyntaxNode? VisitGenericName(GenericNameSyntax node) =>
            _inTypeForwardAttribute && _inTypeOfExpression ?
                node.WithTypeArgumentList(GetOmittedTypeArgumentList(node.TypeArgumentList.Arguments.Count)) :
                node;

        /// <summary>
        /// Replace (TA, TB, TC) with System.ValueTuple<,,> when it occurs in typeof() expression in TypeForwardAttribute.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public override SyntaxNode? VisitTupleType(TupleTypeSyntax node) =>
            _inTypeForwardAttribute && _inTypeOfExpression ?
                SyntaxFactory.GenericName(SyntaxFactory.Identifier("System.ValueTuple"), GetOmittedTypeArgumentList(node.Elements.Count)) :
                node;

        /// <summary>
        /// Checks if the AttributeSyntax's simple name is TypeForwardTo(Attribute).
        /// </summary>
        /// <param name="attributeSyntax"></param>
        /// <returns></returns>
        private static bool IsTypeForwardAttribute(AttributeSyntax? attributeSyntax)
        {
            if (attributeSyntax == null)
            {
                return false;
            }

            ReadOnlySpan<char> attributeSimpleName = GetUnqualifiedName(attributeSyntax.Name).Identifier.ValueText.AsSpan();

            if (attributeSimpleName.EndsWith(nameof(Attribute).AsSpan()))
            {
                attributeSimpleName = attributeSimpleName.Slice(0, attributeSimpleName.Length - nameof(Attribute).Length);
            }
            
            return attributeSimpleName.Equals("TypeForwardedTo".AsSpan(), StringComparison.Ordinal);
        }

        /// <summary>
        /// Get SimpleNameSyntax for base NameSyntax
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private static SimpleNameSyntax GetUnqualifiedName(NameSyntax name)
            => name switch
            {
                AliasQualifiedNameSyntax alias => alias.Name,
                QualifiedNameSyntax qualified => qualified.Right,
                SimpleNameSyntax simple => simple,
                _ => throw new InvalidOperationException("Unreachable"),
            };

        /// <summary>
        /// Create an TypeArgumentListSyntax with <paramref name="argumentCount"/> instances of OmittedTypeArgumentSyntax.
        /// For use in constructing generic type names without named type parameters.
        /// </summary>
        /// <param name="argumentCount"></param>
        /// <returns></returns>
        private TypeArgumentListSyntax GetOmittedTypeArgumentList(int argumentCount)
        {
            SeparatedSyntaxList<TypeSyntax> newArguments = new();

            for (var i = 0; i < argumentCount; i++)
            {
                newArguments = newArguments.Add(SyntaxFactory.OmittedTypeArgument());
            }

            return SyntaxFactory.TypeArgumentList(newArguments);
        }
    }
}
