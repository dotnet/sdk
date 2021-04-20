// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using System.Globalization;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    public class FirstUpperCaseValueFormTests
    {
        [Theory]
        [InlineData("a", "A", null)]
        [InlineData("no", "No", null)]
        [InlineData("new", "New", null)]
        [InlineData("", "", null)]
        [InlineData(null, null, null)]
        [InlineData("indigo", "İndigo", "tr-TR")]
        [InlineData("ındigo", "Indigo", "tr-TR")]
        public void FirstUpperCaseWorksAsExpected(string input, string expected, string culture)
        {
            if (!string.IsNullOrEmpty(culture))
            {
                if (culture == "invariant")
                {
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                }
                else
                {
                    CultureInfo.CurrentCulture = new CultureInfo(culture);
                }
            }

            var model = new FirstUpperCaseValueFormModel();
            string actual = model.Process(null, input);
            Assert.Equal(expected, actual);
        }
    }
}
