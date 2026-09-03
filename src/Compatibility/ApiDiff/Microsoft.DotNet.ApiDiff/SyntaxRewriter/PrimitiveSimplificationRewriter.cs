// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.ApiDiff.SyntaxRewriter;

internal class PrimitiveSimplificationRewriter : CSharpSyntaxRewriter
{
    public static readonly PrimitiveSimplificationRewriter Singleton = new();

    private static readonly Dictionary<string, SyntaxKind> s_primitives = new() {
        { "Boolean", SyntaxKind.BoolKeyword },
        { "Byte", SyntaxKind.ByteKeyword },
        { "Char", SyntaxKind.CharKeyword },
        { "Decimal", SyntaxKind.DecimalKeyword },
        { "Double", SyntaxKind.DoubleKeyword },
        { "Int16", SyntaxKind.ShortKeyword },
        { "Int32", SyntaxKind.IntKeyword },
        { "Int64", SyntaxKind.LongKeyword },
        { "Object", SyntaxKind.ObjectKeyword },
        { "SByte", SyntaxKind.SByteKeyword },
        { "Single", SyntaxKind.FloatKeyword },
        { "String", SyntaxKind.StringKeyword },
        { "UInt16", SyntaxKind.UShortKeyword },
        { "UInt32", SyntaxKind.UIntKeyword },
        { "UInt64", SyntaxKind.ULongKeyword },
        { "System.Boolean", SyntaxKind.BoolKeyword },
        { "System.Byte", SyntaxKind.ByteKeyword },
        { "System.Char", SyntaxKind.CharKeyword },
        { "System.Decimal", SyntaxKind.DecimalKeyword },
        { "System.Double", SyntaxKind.DoubleKeyword },
        { "System.Int16", SyntaxKind.ShortKeyword },
        { "System.Int32", SyntaxKind.IntKeyword },
        { "System.Int64", SyntaxKind.LongKeyword },
        { "System.Object", SyntaxKind.ObjectKeyword },
        { "System.SByte", SyntaxKind.SByteKeyword },
        { "System.Single", SyntaxKind.FloatKeyword },
        { "System.String", SyntaxKind.StringKeyword },
        { "System.UInt16", SyntaxKind.UShortKeyword },
        { "System.UInt32", SyntaxKind.UIntKeyword },
        { "System.UInt64", SyntaxKind.ULongKeyword },
    };

    public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
    {
        if (s_primitives.TryGetValue(node.Right.Identifier.Text, out SyntaxKind keyword))
        {
            return SyntaxFactory.PredefinedType(SyntaxFactory.Token(keyword)).WithTriviaFrom(node);
        }

        return base.VisitQualifiedName(node);
    }
}
