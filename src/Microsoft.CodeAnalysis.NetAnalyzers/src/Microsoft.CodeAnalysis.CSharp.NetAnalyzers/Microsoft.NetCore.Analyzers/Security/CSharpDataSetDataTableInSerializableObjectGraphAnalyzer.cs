// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
