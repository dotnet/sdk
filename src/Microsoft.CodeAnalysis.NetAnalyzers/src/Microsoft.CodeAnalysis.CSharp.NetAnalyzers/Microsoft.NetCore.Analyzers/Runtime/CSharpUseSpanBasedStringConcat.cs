// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpUseSpanBasedStringConcat : UseSpanBasedStringConcat
    {
        private protected override bool IsTopMostConcatOperation(IBinaryOperation binaryOperation)
        {
            return IsConcatOperation(binaryOperation) &&
                (binaryOperation.Parent is not IBinaryOperation parentBinary || !IsConcatOperation(parentBinary));

            static bool IsConcatOperation(IBinaryOperation operation)
            {
                return operation.OperatorKind == BinaryOperatorKind.Add &&
                    operation.Type?.SpecialType == SpecialType.System_String;
            }
        }

        private protected override IOperation WalkDownBuiltInImplicitConversionOnConcatOperand(IOperation operand)
        {
            return CSharpWalkDownBuiltInImplicitConversionOnConcatOperand(operand);
        }
    }
}
