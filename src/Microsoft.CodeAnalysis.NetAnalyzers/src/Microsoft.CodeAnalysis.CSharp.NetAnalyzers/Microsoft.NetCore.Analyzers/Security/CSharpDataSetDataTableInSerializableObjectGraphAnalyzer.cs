// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Security;

namespace Microsoft.NetCore.CSharp.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpDataSetDataTableInSerializableObjectGraphAnalyzer
        : DataSetDataTableInSerializableObjectGraphAnalyzer
    {
        protected override string ToString(TypedConstant typedConstant)
            => typedConstant.ToCSharpString();
    }
}
