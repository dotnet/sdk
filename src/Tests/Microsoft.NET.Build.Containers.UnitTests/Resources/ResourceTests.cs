// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Globalization;
using System.Resources;
using Microsoft.NET.Build.Containers.Resources;
using Xunit;

namespace Microsoft.NET.Build.Containers.UnitTests.Resources
{
    public class ResourceTests
    {
        [Fact]
        public void GetString_ReturnsValueFromResources()
        {
            Assert.Equal("CONTAINER0000: Value for unit test {0}", Resource.GetString(nameof(Strings._Test)));
        }

        [Fact]
        public void FormatString_ReturnsValueFromResources()
        {
            Assert.Equal("CONTAINER0000: Value for unit test 1", Resource.FormatString(nameof(Strings._Test), 1));
        }

        [Fact]
        public void EnsureErrorCodeUniqueness()
        {
            ResourceSet? resourceSet = Resource.Manager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
            Assert.NotNull(resourceSet);

            IEnumerable<IGrouping<string, DictionaryEntry>> groups = resourceSet
                .OfType<DictionaryEntry>()
                .Where(e => e.Value is string value && value.StartsWith("CONTAINER", StringComparison.OrdinalIgnoreCase))
                .GroupBy(e => e.Value!.ToString()!.Substring(9, 4))
                .Where(g => g.Count() > 1);

            foreach(IGrouping<string, DictionaryEntry> group in groups)
            {
                if (!group.First().Key!.ToString()!.Contains('_'))
                {
                    Assert.Fail($"Code with number '{group.Key}' is used for multiple resources. You can use single code for multiple messages, but the name of these resources must share same prefix delimited by underscore.");
                }
                else
                {
                    string prefix = group.First().Key!.ToString()!.Split('_')[0];

                    Assert.All(group, e => e.Key!.ToString()!.StartsWith(prefix, StringComparison.Ordinal));
                }
            }
        }
    }
}
