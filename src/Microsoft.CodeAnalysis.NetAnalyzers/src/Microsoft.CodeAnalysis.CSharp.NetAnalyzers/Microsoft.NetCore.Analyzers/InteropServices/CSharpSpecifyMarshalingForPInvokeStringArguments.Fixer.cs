// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Microsoft.NetCore.Analyzers.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.NetCore.CSharp.Analyzers.InteropServices
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = PInvokeDiagnosticAnalyzer.RuleCA2101Id), Shared]
    public class CSharpSpecifyMarshalingForPInvokeStringArgumentsFixer : SpecifyMarshalingForPInvokeStringArgumentsFixer
    {
        protected override bool IsAttribute(SyntaxNode node)
        {
            return node.IsKind(SyntaxKind.Attribute);
        }

        protected override SyntaxNode FindNamedArgument(IReadOnlyList<SyntaxNode> arguments, string argumentName)
        {
            return arguments.OfType<AttributeArgumentSyntax>().FirstOrDefault(arg => arg.NameEquals != null && arg.NameEquals.Name.Identifier.Text == argumentName);
        }

        protected override bool IsDeclareStatement(SyntaxNode node)
        {
            return false;
        }

        protected override Task<Document> FixDeclareStatementAsync(Document document, SyntaxNode node, CancellationToken cancellationToken)
        {
            return Task.FromResult(document);
        }
    }
}
