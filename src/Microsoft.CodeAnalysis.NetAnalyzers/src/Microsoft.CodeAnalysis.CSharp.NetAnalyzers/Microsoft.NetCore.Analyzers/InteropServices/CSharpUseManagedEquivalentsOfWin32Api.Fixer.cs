// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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