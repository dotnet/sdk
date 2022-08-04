// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using System.Globalization;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
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

            var model = new FirstUpperCaseValueFormFactory().Create("test");
            string? actual = model.Process(input, new Dictionary<string, IValueForm>());
            Assert.Equal(expected, actual);
        }
    }
}
