// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    public class TitleCaseValueFormTests
    {
        [Theory]
        [InlineData("project x", "Project X")]
        [InlineData("x project x", "X Project X")]
        [InlineData("new project name", "New Project Name")]
        [InlineData("new-project%name", "New-Project%Name")]
        [InlineData("", "")]
        [InlineData(null, null)]
        public void TitleCaseWorksAsExpected(string input, string expected)
        {
            var model = new TitleCaseValueFormModel();
            string actual = model.Process(null, input);
            Assert.Equal(expected, actual);
        }
    }
}
