using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    public class FirstUpperCaseValueFormTests
    {
        [Theory]
        [InlineData("a", "A")]
        [InlineData("no", "No")]
        [InlineData("new", "New")]
        [InlineData("", "")]
        [InlineData(null, null)]
        public void FirstUpperCaseWorksAsExpected(string input, string expected)
        {
            var model = new FirstUpperCaseValueFormModel();
            string actual = model.Process(null, input);
            Assert.Equal(expected, actual);
        }
    }
}
