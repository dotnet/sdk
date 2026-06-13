// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    [TestClass]
    public class CombinedListTests
    {
        [TestMethod]
        [DataRow(new int[] { }, new int[] { })]
        [DataRow(new int[] { 5 }, new int[] { })]
        [DataRow(new int[] { }, new int[] { 3 })]
        [DataRow(new int[] { 1, 2 }, new int[] { })]
        [DataRow(new int[] { }, new int[] { 1, 2 })]
        [DataRow(new int[] { 1 }, new int[] { 1 })]
        [DataRow(new int[] { 1 }, new int[] { 1, 2, 3 })]
        [DataRow(new int[] { 1, 2, 3 }, new int[] { 1 })]
        [DataRow(new int[] { 1, 2 }, new int[] { 1, 2, 3 })]
        [DataRow(new int[] { 1, 2, 3, 4, 5 }, new int[] { 1, 2, 3 })]
        public void VerifyCombinedListCombinesCorrectly(IReadOnlyList<int> listOne, IReadOnlyList<int> listTwo)
        {
            CombinedList<int> combined = new CombinedList<int>(listOne, listTwo);

            List<int> manuallyAppended = new List<int>();
            manuallyAppended.AddRange(listOne);
            manuallyAppended.AddRange(listTwo);

            Assert.AreEqual(combined.Count, manuallyAppended.Count);

            int enumerationCount = 0;
            foreach (int value in combined)
            {
                enumerationCount++;
            }

            Assert.AreEqual(enumerationCount, combined.Count);
        }
    }
}
