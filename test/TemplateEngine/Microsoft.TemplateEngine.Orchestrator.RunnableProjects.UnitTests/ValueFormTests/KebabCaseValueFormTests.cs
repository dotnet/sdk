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
        [InlineData("", "")]
        [InlineData(null, null)]

        public void KebabCaseWorksAsExpected(string input, string expected)
        {
            var model = new KebabCaseValueFormModel();
            string actual = model.Process(null, input);
            Assert.Equal(expected, actual);
        }
    }
}
