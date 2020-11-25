using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    public class ListExtensionsTests
    {
        internal struct GroupByTestStruct
        {
            internal string Identity;
            internal string GroupIdentity;
        }

        [Fact(DisplayName = nameof(GroupByExtensionTest))]
        public void GroupByExtensionTest()
        {
            List<GroupByTestStruct> templatesToGroup = new List<GroupByTestStruct>();
            templatesToGroup.Add(new GroupByTestStruct()
            {
                Identity = "1",
                GroupIdentity = null
            });
            templatesToGroup.Add(new GroupByTestStruct()
            {
                Identity = "2",
                GroupIdentity = string.Empty
            });
            templatesToGroup.Add(new GroupByTestStruct()
            {
                Identity = "3",
                GroupIdentity = null
            });
            templatesToGroup.Add(new GroupByTestStruct()
            {
                Identity = "4",
                GroupIdentity = string.Empty
            });
            templatesToGroup.Add(new GroupByTestStruct()
            {
                Identity = "5",
                GroupIdentity = "TemplateGroup"
            });
            templatesToGroup.Add(new GroupByTestStruct()
            {
                Identity = "6",
                GroupIdentity = "templategroup"
            });
            templatesToGroup.Add(new GroupByTestStruct()
            {
                Identity = "7",
                GroupIdentity = "TemplateGroup2"
            });
            templatesToGroup.Add(new GroupByTestStruct()
            {
                Identity = "8",
                GroupIdentity = "other"
            });
            templatesToGroup.Add(new GroupByTestStruct()
            {
                Identity = "9",
                GroupIdentity = "templategroup"
            });

            var templateGroups = templatesToGroup.GroupBy(x => x.GroupIdentity, x => !string.IsNullOrEmpty(x.GroupIdentity), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(7, templateGroups.Count());
            var groupWithExpectedMultipleElements = templateGroups.Single(g => g.Key?.Equals("TemplateGroup", StringComparison.OrdinalIgnoreCase) ?? false); 
            Assert.Equal(3, groupWithExpectedMultipleElements.Count());
            Assert.Single(groupWithExpectedMultipleElements, s => s.Identity == "5");
            Assert.Single(groupWithExpectedMultipleElements, s => s.Identity == "6");
            Assert.Single(groupWithExpectedMultipleElements, s => s.Identity == "9");
        }
    }
}
