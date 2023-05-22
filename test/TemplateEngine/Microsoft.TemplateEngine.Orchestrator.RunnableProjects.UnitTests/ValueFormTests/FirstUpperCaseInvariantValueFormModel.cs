// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    public class FirstUpperCaseInvariantValueFormTests
    {
        [Theory]
        [InlineData("a", "A", null)]
        [InlineData("no", "No", null)]
        [InlineData("new", "New", null)]
        [InlineData("", "", null)]
        [InlineData("indigo", "Indigo", "tr-TR")]
        [InlineData("ındigo", "ındigo", "tr-TR")]
        public void FirstUpperCaseInvariantWorksAsExpected(string input, string expected, string? culture)
        {
            if (!string.IsNullOrEmpty(culture))
            {
                CultureInfo.CurrentCulture = culture == "invariant" ? CultureInfo.InvariantCulture : new CultureInfo(culture);
            }
            IValueForm model = new FirstUpperCaseInvariantValueFormFactory().Create("test");
            string actual = model.Process(input, new Dictionary<string, IValueForm>());
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CanHandleNullValue()
        {
            IValueForm model = new FirstUpperCaseInvariantValueFormFactory().Create("test");
            Assert.Throws<ArgumentNullException>(() => model.Process(null!, new Dictionary<string, IValueForm>()));
        }
    }
}
