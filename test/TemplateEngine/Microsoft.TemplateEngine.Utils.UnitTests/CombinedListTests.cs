// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    public class CombinedListTests
    {
        [Theory(DisplayName = nameof(VerifyCombinedListCombinesCorrectly))]
        [InlineData(new int[] { }, new int[] { })]
        [InlineData(new int[] { 5 }, new int[] { })]
        [InlineData(new int[] { }, new int[] { 3 })]
        [InlineData(new int[] { 1, 2 }, new int[] { })]
        [InlineData(new int[] { }, new int[] { 1, 2})]
        [InlineData(new int[] { 1 }, new int[] { 1 })]
        [InlineData(new int[] { 1 }, new int[] { 1, 2, 3})]
        [InlineData(new int[] { 1, 2, 3 }, new int[] { 1 })]
        [InlineData(new int[] { 1, 2 }, new int[] { 1, 2, 3 })]
        [InlineData(new int[] { 1, 2, 3, 4, 5 }, new int[] { 1, 2, 3 })]

        public void VerifyCombinedListCombinesCorrectly(IReadOnlyList<int> listOne, IReadOnlyList<int> listTwo)
        {
            CombinedList<int> combined = new CombinedList<int>(listOne, listTwo);

            List<int> manuallyAppended = new List<int>();
            manuallyAppended.AddRange(listOne);
            manuallyAppended.AddRange(listTwo);

            Assert.Equal(combined.Count, manuallyAppended.Count);

            int enumerationCount = 0;
            foreach (int value in combined)
            {
                enumerationCount++;
            }

            Assert.Equal(enumerationCount, combined.Count);
        }
    }
}
