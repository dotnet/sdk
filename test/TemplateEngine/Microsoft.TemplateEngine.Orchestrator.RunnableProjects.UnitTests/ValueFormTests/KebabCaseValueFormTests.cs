// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    [TestClass]
    public class KebabCaseValueFormTests
    {
        [TestMethod]
        [DataRow("I", "i")]
        [DataRow("IO", "io")]
        [DataRow("FileIO", "file-io")]
        [DataRow("SignalR", "signal-r")]
        [DataRow("IOStream", "io-stream")]
        [DataRow("COMObject", "com-object")]
        [DataRow("WebAPI", "web-api")]
        [DataRow("XProjectX", "x-project-x")]
        [DataRow("NextXXXProject", "next-xxx-project")]
        [DataRow("NoNewProject", "no-new-project")]
        [DataRow("NONewProject", "no-new-project")]
        [DataRow("NewProjectName", "new-project-name")]
        [DataRow("ABBREVIATIONAndSomeName", "abbreviation-and-some-name")]
        [DataRow("NoNoNoNoNoNoNoName", "no-no-no-no-no-no-no-name")]
        [DataRow("AnotherNewNewNewNewNewProjectName", "another-new-new-new-new-new-project-name")]
        [DataRow("Param1TestValue", "param-1-test-value")]
        [DataRow("Windows10", "windows-10")]
        [DataRow("WindowsServer2016R2", "windows-server-2016-r-2")]
        [DataRow("", "")]
        [DataRow(";MyWord;", "my-word")]
        [DataRow("My Word", "my-word")]
        [DataRow("My    Word", "my-word")]
        [DataRow(";;;;;", "")]
        [DataRow("       ", "")]
        [DataRow("Simple TEXT_here", "simple-text-here")]
        [DataRow("НоваяПеременная", "новая-переменная")]
        public void KebabCaseWorksAsExpected(string input, string expected)
        {
            IValueForm? model = new KebabCaseValueFormFactory().Create("test");
            string actual = model.Process(input, new Dictionary<string, IValueForm>());
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void CanHandleNullValue()
        {
            IValueForm model = new KebabCaseValueFormFactory().Create("test");
            Assert.ThrowsExactly<ArgumentNullException>(() => model.Process(null!, new Dictionary<string, IValueForm>()));
        }
    }
}
