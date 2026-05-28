// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    public class ListExtensionsTests
    {
        [Fact(DisplayName = nameof(GroupByExtensionTest))]
        public void GroupByExtensionTest()
        {
            List<GroupByTestStruct> templatesToGroup = new List<GroupByTestStruct>
            {
                new GroupByTestStruct()
                {
                    _identity = "1",
                    _groupIdentity = null
                },
                new GroupByTestStruct()
                {
                    _identity = "2",
                    _groupIdentity = string.Empty
                },
                new GroupByTestStruct()
                {
                    _identity = "3",
                    _groupIdentity = null
                },
                new GroupByTestStruct()
                {
                    _identity = "4",
                    _groupIdentity = string.Empty
                },
                new GroupByTestStruct()
                {
                    _identity = "5",
                    _groupIdentity = "TemplateGroup"
                },
                new GroupByTestStruct()
                {
                    _identity = "6",
                    _groupIdentity = "templategroup"
                },
                new GroupByTestStruct()
                {
                    _identity = "7",
                    _groupIdentity = "TemplateGroup2"
                },
                new GroupByTestStruct()
                {
                    _identity = "8",
                    _groupIdentity = "other"
                },
                new GroupByTestStruct()
                {
                    _identity = "9",
                    _groupIdentity = "templategroup"
                }
            };

            var templateGroups = templatesToGroup.GroupBy(x => x._groupIdentity, x => !string.IsNullOrEmpty(x._groupIdentity), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(7, templateGroups.Count());
            var groupWithExpectedMultipleElements = templateGroups.Single(g => g.Key?.Equals("TemplateGroup", StringComparison.OrdinalIgnoreCase) ?? false);
            Assert.Equal(3, groupWithExpectedMultipleElements.Count());
            Assert.Single(groupWithExpectedMultipleElements, s => s._identity == "5");
            Assert.Single(groupWithExpectedMultipleElements, s => s._identity == "6");
            Assert.Single(groupWithExpectedMultipleElements, s => s._identity == "9");
        }

        internal struct GroupByTestStruct
        {
            internal string? _identity;
            internal string? _groupIdentity;
        }
    }
}
