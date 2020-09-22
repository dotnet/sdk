// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public sealed class CSharpPreferStreamAsyncMemoryOverloadsFixer : PreferStreamAsyncMemoryOverloadsFixer
    {
        protected override IArgumentOperation? GetArgumentByPositionOrName(ImmutableArray<IArgumentOperation> args, int index, string name, out bool isNamed)
        {
            isNamed = false;

            // The expected position is beyond the total arguments, so we don't expect to find the argument in the array
            if (index >= args.Length)
            {
                return null;
            }
            // If the argument in the specified index does not have a name, then it is in its expected position
            else if (args[index].Syntax is ArgumentSyntax argNode && argNode.NameColon == null)
            {
                return args[index];
            }
            // Otherwise, find it by name
            else
            {
                isNamed = true;
                return args.FirstOrDefault(argOperation =>
                {
                    return argOperation.Syntax is ArgumentSyntax argNode &&
                           argNode.NameColon?.Name?.Identifier.ValueText == name;
                });
            }
        }

        protected override bool IsSystemNamespaceImported(IReadOnlyList<SyntaxNode> importList)
        {
            foreach (SyntaxNode import in importList)
            {
                if (import is UsingDirectiveSyntax { Name: IdentifierNameSyntax { Identifier: { Text: nameof(System) } } })
                {
                    return true;
                }
            }
            return false;
        }
    }
}
