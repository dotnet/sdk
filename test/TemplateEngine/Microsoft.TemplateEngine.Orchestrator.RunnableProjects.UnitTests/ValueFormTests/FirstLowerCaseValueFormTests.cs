// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using System.Globalization;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    public class FirstLowerCaseValueFormTests
    {
        [Theory]
        [InlineData("A", "a", null)]
        [InlineData("NO", "nO", null)]
        [InlineData("NEW", "nEW", null)]
        [InlineData("", "", null)]
        [InlineData(null, null, null)]
        [InlineData("İndigo", "indigo", "tr-TR")]
        [InlineData("Indigo", "ındigo", "tr-TR")]
        public void FirstLowerCaseWorksAsExpected(string input, string expected, string culture)
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

            var model = new FirstLowerCaseValueFormModel();
            string actual = model.Process(null, input);
            Assert.Equal(expected, actual);
        }
    }
}
