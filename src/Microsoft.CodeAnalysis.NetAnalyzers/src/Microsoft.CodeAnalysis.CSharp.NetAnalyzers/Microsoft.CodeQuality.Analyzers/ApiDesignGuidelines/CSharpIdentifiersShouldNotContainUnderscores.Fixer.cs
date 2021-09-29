// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1707: Identifiers should not contain underscores
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpIdentifiersShouldNotContainUnderscoresFixer : IdentifiersShouldNotContainUnderscoresFixer
    {
        protected override string GetNewName(string name)
        {
            string result = RemoveUnderscores(name);
            if (result.Length == 0)
            {
                return string.Empty;
            }

            if (!SyntaxFacts.IsValidIdentifier(result))
            {
                return $"@{result}";
            }

            return result;
        }

        protected override SyntaxNode GetDeclarationNode(SyntaxNode node)
            => node.IsKind(SyntaxKind.IdentifierName)
                ? node.Parent
                : node;
    }
}
