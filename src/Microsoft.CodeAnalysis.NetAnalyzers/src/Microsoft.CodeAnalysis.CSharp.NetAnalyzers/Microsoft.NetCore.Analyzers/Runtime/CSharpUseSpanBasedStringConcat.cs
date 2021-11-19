// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpUseSpanBasedStringConcat : UseSpanBasedStringConcat
    {
        private protected override bool TryGetTopMostConcatOperation(IBinaryOperation binaryOperation, [NotNullWhen(true)] out IBinaryOperation? rootBinaryOperation)
        {
            if (!IsConcatOperation(binaryOperation))
            {
                rootBinaryOperation = default;
                return false;
            }

            var current = binaryOperation;
            while (current.Parent is IBinaryOperation parentBinaryOperation && IsConcatOperation(parentBinaryOperation))
                current = parentBinaryOperation;

            rootBinaryOperation = current;
            return true;

            static bool IsConcatOperation(IBinaryOperation operation)
            {
                return operation.OperatorKind == BinaryOperatorKind.Add &&
                    operation.Type.SpecialType == SpecialType.System_String;
            }
        }

        private protected override IOperation WalkDownBuiltInImplicitConversionOnConcatOperand(IOperation operand)
        {
            return CSharpWalkDownBuiltInImplicitConversionOnConcatOperand(operand);
        }
    }
}
