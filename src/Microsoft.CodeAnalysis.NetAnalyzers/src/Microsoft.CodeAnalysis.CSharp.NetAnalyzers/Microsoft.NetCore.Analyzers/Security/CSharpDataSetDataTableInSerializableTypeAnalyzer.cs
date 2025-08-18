// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
