// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.NetCore.Analyzers.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.NetCore.CSharp.Analyzers.InteropServices
{
    /// <summary>
    /// CA2205: Use managed equivalents of win32 api
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public class CSharpUseManagedEquivalentsOfWin32ApiFixer : UseManagedEquivalentsOfWin32ApiFixer
    {
    }
}