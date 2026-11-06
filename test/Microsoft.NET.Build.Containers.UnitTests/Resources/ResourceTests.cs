// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Globalization;
using System.Resources;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.UnitTests.Resources
{
    [TestClass]
    public class ResourceTests
    {
        [TestMethod]
        public void GetString_ReturnsValueFromResources()
        {
            Assert.AreEqual("CONTAINER0000: Value for unit test {0}", Resource.GetString(nameof(Strings._Test)));
        }

        [TestMethod]
        public void FormatString_ReturnsValueFromResources()
        {
            Assert.AreEqual("CONTAINER0000: Value for unit test 1", Resource.FormatString(nameof(Strings._Test), 1));
        }

        [TestMethod]
        public void EnsureErrorCodeUniqueness()
        {
            ResourceSet? resourceSet = Resource.Manager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
            Assert.IsNotNull(resourceSet);

            IEnumerable<IGrouping<string, DictionaryEntry>> groups = resourceSet
                .OfType<DictionaryEntry>()
                .Where(e => e.Value is string value && value.StartsWith("CONTAINER", StringComparison.OrdinalIgnoreCase))
                .GroupBy(e => e.Value!.ToString()!.Substring(9, 4))
                .Where(g => g.Count() > 1);

            foreach (IGrouping<string, DictionaryEntry> group in groups)
            {
                if (!group.First().Key!.ToString()!.Contains('_'))
                {
                    Assert.Fail($"Code with number '{group.Key}' is used for multiple resources. You can use single code for multiple messages, but the name of these resources must share same prefix delimited by underscore.");
                }
                else
                {
                    string prefix = group.First().Key!.ToString()!.Split('_')[0];

                    foreach (DictionaryEntry entry in group)
                    {
                        Assert.StartsWith(prefix, entry.Key!.ToString()!, StringComparison.Ordinal);
                    }
                }
            }
        }
    }
}
