// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    [TestClass]
    public class ValueContentAbstractValueTests
    {
        [TestMethod]
        [DataRow(5, 2, 100, BinaryOperatorKind.Add, 10)]
        [DataRow(6, 2, 100, BinaryOperatorKind.Add, 0)]
        [DataRow(10, 10, 1, BinaryOperatorKind.And, 10)]
        public void BinaryOperationBoundsDistinctResults(
            int leftCount, int rightCount, int rightMultiplier, BinaryOperatorKind operatorKind, int expectedCount)
        {
            var compilation = CSharpCompilation.Create("Test");
            var type = compilation.GetSpecialType(SpecialType.System_Int32);
            var left = ValueContentAbstractValue.UndefinedState;
            var right = ValueContentAbstractValue.UndefinedState;
            for (int i = 0; i < leftCount; i++)
            {
                left = left.Merge(ValueContentAbstractValue.Create(i, type));
            }

            for (int i = 0; i < rightCount; i++)
            {
                right = right.Merge(ValueContentAbstractValue.Create(i * rightMultiplier, type));
            }

            var result = left.MergeBinaryOperation(right, operatorKind, type, type, type);

            if (expectedCount == 0)
            {
                Assert.AreSame(ValueContentAbstractValue.MayBeContainsNonLiteralState, result);
            }
            else
            {
                Assert.IsTrue(result.IsLiteralState);
                var expected = Enumerable.Range(0, leftCount)
                    .SelectMany(l => Enumerable.Range(0, rightCount)
                        .Select(r => operatorKind == BinaryOperatorKind.Add ? l + r * rightMultiplier : l & r))
                    .Distinct()
                    .Cast<object>()
                    .ToArray();
                Assert.HasCount(expectedCount, result.LiteralValues);
                Assert.IsTrue(result.LiteralValues.SetEquals(expected));
            }
        }
    }
}
