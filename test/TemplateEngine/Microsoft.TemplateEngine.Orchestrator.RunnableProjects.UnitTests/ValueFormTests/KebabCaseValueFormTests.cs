// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    public class KebabCaseValueFormTests
    {
        [Theory]
        [InlineData("I", "i")]
        [InlineData("IO", "io")]
        [InlineData("FileIO", "file-io")]
        [InlineData("SignalR", "signal-r")]
        [InlineData("IOStream", "io-stream")]
        [InlineData("COMObject", "com-object")]
        [InlineData("WebAPI", "web-api")]
        [InlineData("XProjectX", "x-project-x")]
        [InlineData("NextXXXProject", "next-xxx-project")]
        [InlineData("NoNewProject", "no-new-project")]
        [InlineData("NONewProject", "no-new-project")]
        [InlineData("NewProjectName", "new-project-name")]
        [InlineData("ABBREVIATIONAndSomeName", "abbreviation-and-some-name")]
        [InlineData("NoNoNoNoNoNoNoName", "no-no-no-no-no-no-no-name")]
        [InlineData("AnotherNewNewNewNewNewProjectName", "another-new-new-new-new-new-project-name")]
        [InlineData("Param1TestValue", "param-1-test-value")]
        [InlineData("Windows10", "windows-10")]
        [InlineData("WindowsServer2016R2", "windows-server-2016-r-2")]
        [InlineData("", "")]
        [InlineData(null, null)]
        [InlineData(";MyWord;", "my-word")]
        [InlineData("My Word", "my-word")]
        [InlineData("My    Word", "my-word")]
        [InlineData(";;;;;", "")]
        [InlineData ("       ", "")]
        [InlineData("Simple TEXT_here", "simple-text-here")]
        [InlineData("НоваяПеременная", "новая-переменная")]


        public void KebabCaseWorksAsExpected(string input, string expected)
        {
            var model = new KebabCaseValueFormModel();
            string actual = model.Process(null, input);
            Assert.Equal(expected, actual);
        }
    }
}
