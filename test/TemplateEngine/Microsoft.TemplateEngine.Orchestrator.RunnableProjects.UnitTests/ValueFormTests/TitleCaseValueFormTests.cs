// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    [TestClass]
    public class TitleCaseValueFormTests
    {
        [TestMethod]
        [DataRow("project x", "Project X")]
        [DataRow("x project x", "X Project X")]
        [DataRow("new project name", "New Project Name")]
        [DataRow("new-project%name", "New-Project%Name")]
        [DataRow("", "")]
        public void TitleCaseWorksAsExpected(string input, string expected)
        {
            IValueForm model = new TitleCaseValueFormFactory().Create("test");
            string actual = model.Process(input, new Dictionary<string, IValueForm>());
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void CanHandleNullValue()
        {
            IValueForm model = new TitleCaseValueFormFactory().Create("test");
            Assert.ThrowsExactly<ArgumentNullException>(() => model.Process(null!, new Dictionary<string, IValueForm>()));
        }
    }
}
