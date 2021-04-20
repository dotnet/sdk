// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    public class EqualityExtensionsTests
    {
        [Fact(DisplayName = "AllAreTheSameDefaultComparerTrueTest")]
        public void AllAreTheSameDefaultComparerTrueTest()
        {
            IDictionary<int, string> items = new Dictionary<int, string>()
            {
                { 1, "this" },
                { 2, "this" },
                { 3, "this" },
                { 4, "this" }
            };
            Func<KeyValuePair<int, string>, string> selector = (KeyValuePair<int, string> x) => x.Value;

            Assert.True(items.AllAreTheSame(selector));
        }

        [Fact(DisplayName = "AllAreTheSameDefaultComparerFailsTest")]
        public void AllAreTheSameDefaultComparerFailsTest()
        {
            IDictionary<int, string> items = new Dictionary<int, string>()
            {
                { 1, "this" },
                { 2, "that" },
                { 3, "other" },
                { 4, "thing" }
            };
            Func<KeyValuePair<int, string>, string> selector = (KeyValuePair<int, string> x) => x.Value;

            Assert.False(items.AllAreTheSame(selector));
        }

        [Fact(DisplayName = "AllAreTheSameCustomComparerTest")]
        public void AllAreTheSameCustomComparerTest()
        {
            IDictionary<int, string> items = new Dictionary<int, string>()
            {
                { 1, "this" },
                { 2, "that" },
                { 3, "four" },
                { 4, "long" }
            };
            Func<KeyValuePair<int, string>, string> selector = (KeyValuePair<int, string> x) => x.Value;

            Func<string, string, bool> lengthComparer = (x, y) => x.Length == y.Length;

            // they're all the same length
            Assert.True(items.AllAreTheSame(selector, lengthComparer));

            Func<string, string, bool> upperComparer = (x, y) => x.ToUpper() == y.ToUpper();
            Assert.False(items.AllAreTheSame(selector, upperComparer));
        }
    }
}
