// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.NetCore.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.CSharp.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpDataSetDataTableInSerializableTypeAnalyzer
        : DataSetDataTableInSerializableTypeAnalyzer
    {
        protected override string ToString(TypedConstant typedConstant)
            => typedConstant.ToCSharpString();
    }
}
