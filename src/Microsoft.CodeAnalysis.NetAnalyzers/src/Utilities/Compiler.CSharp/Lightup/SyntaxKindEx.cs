// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.CSharp;

namespace Analyzer.Utilities.Lightup
{
    internal static class SyntaxKindEx
    {
        // https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/Syntax/SyntaxKind.cs
        public const SyntaxKind ExtensionBlockDeclaration = (SyntaxKind)9079;
    }
}
