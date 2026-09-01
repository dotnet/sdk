// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NetFramework.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetFramework.CSharp.Analyzers
{
    /// <summary>
    /// CA2212: Do not mark serviced components with WebMethod
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpDoNotMarkServicedComponentsWithWebMethodAnalyzer : DoNotMarkServicedComponentsWithWebMethodAnalyzer
    {
    }
}