// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = "CA2237 CodeFix provider"), Shared]
    public class CSharpMarkAllNonSerializableFieldsFixer : MarkAllNonSerializableFieldsFixer
    {
        protected override SyntaxNode? GetFieldDeclarationNode(SyntaxNode node)
        {
            SyntaxNode? fieldNode = node;
            while (fieldNode != null && !fieldNode.IsKind(SyntaxKind.FieldDeclaration))
            {
                fieldNode = fieldNode.Parent;
            }

            return fieldNode.IsKind(SyntaxKind.FieldDeclaration) ? fieldNode : null;
        }
    }
}
