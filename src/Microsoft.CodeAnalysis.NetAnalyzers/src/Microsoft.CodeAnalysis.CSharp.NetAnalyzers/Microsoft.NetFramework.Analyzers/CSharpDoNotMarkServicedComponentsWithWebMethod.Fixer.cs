// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.NetFramework.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.NetFramework.CSharp.Analyzers
{
    /// <summary>
    /// CA2212: Do not mark serviced components with WebMethod
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public class CSharpDoNotMarkServicedComponentsWithWebMethodFixer : DoNotMarkServicedComponentsWithWebMethodFixer
    {
    }
}