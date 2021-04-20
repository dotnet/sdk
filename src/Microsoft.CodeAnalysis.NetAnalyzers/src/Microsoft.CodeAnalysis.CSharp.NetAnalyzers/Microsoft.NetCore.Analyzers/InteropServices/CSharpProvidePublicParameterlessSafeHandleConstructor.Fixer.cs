// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.NetCore.Analyzers.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.NetCore.CSharp.Analyzers.InteropServices
{
    /// <summary>
    /// CA1414: Provide a public parameterless constructor for concrete types derived from System.Runtime.InteropServices.SafeHandle
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpProvidePublicParameterlessSafeHandleConstructorFixer : ProvidePublicParameterlessSafeHandleConstructorFixer
    {
    }
}