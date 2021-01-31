// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpUseSpanBasedStringConcat : UseSpanBasedStringConcat
    {
        private protected override bool IsStringConcatOperation(IBinaryOperation binaryOperation)
        {
            return binaryOperation.OperatorKind == BinaryOperatorKind.Add &&
                binaryOperation.Type.SpecialType == SpecialType.System_String;
        }
    }
}
