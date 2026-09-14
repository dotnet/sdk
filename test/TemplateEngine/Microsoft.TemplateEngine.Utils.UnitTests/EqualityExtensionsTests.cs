// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    [TestClass]
    public class EqualityExtensionsTests
    {
        [TestMethod]
        public void AllAreTheSameDefaultComparerTrueTest()
        {
            IDictionary<int, string> items = new Dictionary<int, string>()
            {
                { 1, "this" },
                { 2, "this" },
                { 3, "this" },
                { 4, "this" }
            };
            static string Selector(KeyValuePair<int, string> x) => x.Value;

            Assert.IsTrue(items.AllAreTheSame(Selector));
        }

        [TestMethod]
        public void AllAreTheSameDefaultComparerFailsTest()
        {
            IDictionary<int, string> items = new Dictionary<int, string>()
            {
                { 1, "this" },
                { 2, "that" },
                { 3, "other" },
                { 4, "thing" }
            };
            static string Selector(KeyValuePair<int, string> x) => x.Value;

            Assert.IsFalse(items.AllAreTheSame(Selector));
        }

        [TestMethod]
        public void AllAreTheSameCustomComparerTest()
        {
            IDictionary<int, string> items = new Dictionary<int, string>()
            {
                { 1, "this" },
                { 2, "that" },
                { 3, "four" },
                { 4, "long" }
            };
            static string Selector(KeyValuePair<int, string> x) => x.Value;

            static bool LengthComparer(string? x, string? y) => x!.Length == y!.Length;

            // they're all the same length
            Assert.IsTrue(items.AllAreTheSame(Selector, LengthComparer));

            static bool UpperComparer(string? x, string? y) => x!.ToUpper() == y!.ToUpper();
            Assert.IsFalse(items.AllAreTheSame(Selector, UpperComparer));
        }
    }
}
