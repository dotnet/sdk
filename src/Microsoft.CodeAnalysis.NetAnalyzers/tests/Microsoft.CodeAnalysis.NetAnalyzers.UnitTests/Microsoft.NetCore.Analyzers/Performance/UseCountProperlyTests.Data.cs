// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public sealed class BinaryExpressionTestData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            // The operators that we want to test.
            foreach (OperatorKind @operator in _operators)
            {
                // Whether Count {operator} {literal} OR {literal} {operator} Count.
                foreach (bool isRightSideExpression in new bool[] { false, true })
                {
                    // The literals or constants we want to test against.
                    foreach (int literal in new int[] { 0, 1 })
                    {
                        bool resultWhenExpressionEqualsZero = default;
                        bool noDiagnosis = false;
                        // Simulate a value that Count might have in order to determine what's the expected behavior in such case.
                        foreach (int expressionValue in new int[] { 0, 1, 2 })
                        {
                            int rightSide = isRightSideExpression ? expressionValue : literal;
                            int leftSide = isRightSideExpression ? literal : expressionValue;

                            if (expressionValue == 0)
                            {
                                resultWhenExpressionEqualsZero = @operator.Operation(leftSide, rightSide);
                            }
                            else
                            {
                                // If the evaluation for Count {operand} 0 when Count = 1 has the same result as the evaluation
                                // when either Count = 1 or Count = 2 then the case does not apply and therefore no diagnosis should be given.
                                if (resultWhenExpressionEqualsZero == @operator.Operation(leftSide, rightSide))
                                {
                                    noDiagnosis = true;
                                    break;
                                }
                            }
                        }

                        yield return new object[]
                        {
                            noDiagnosis,
                            literal,
                            @operator.BinaryOperatorKind,
                            isRightSideExpression,
                            // Indicates whether the IsEmpty property should be negated on the fix.
                            !resultWhenExpressionEqualsZero
                        };
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static readonly List<OperatorKind> _operators = new()
        {
            new OperatorKind((a, b) => a == b, 1, 2, BinaryOperatorKind.Equals),
            new OperatorKind((a, b) => a != b, 2, 2, BinaryOperatorKind.NotEquals),
            new OperatorKind((a, b) => a > b, 1, 1, BinaryOperatorKind.GreaterThan),
            new OperatorKind((a, b) => a >= b, 2, 2, BinaryOperatorKind.GreaterThanOrEqual),
            new OperatorKind((a, b) => a < b, 1, 1, BinaryOperatorKind.LessThan),
            new OperatorKind((a, b) => a <= b, 2, 2, BinaryOperatorKind.LessThanOrEqual),
        };
    }

#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct OperatorKind
    {
        public int BasicOperatorLength { get; }
        public int CSharpOperatorLength { get; }
        public Func<int, int, bool> Operation { get; }
        public BinaryOperatorKind BinaryOperatorKind { get; }

        public OperatorKind(Func<int, int, bool> operation, int basicOperatorLength, int csharpOperatorLength, BinaryOperatorKind operatorKind)
        {
            Operation = operation;
            BinaryOperatorKind = operatorKind;
            BasicOperatorLength = basicOperatorLength;
            CSharpOperatorLength = csharpOperatorLength;
        }
    }
}
