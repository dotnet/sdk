using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    public class FirstLowerCaseValueFormTests
    {
        [Theory]
        [InlineData("A", "a")]
        [InlineData("NO", "nO")]
        [InlineData("NEW", "nEW")]
        [InlineData("", "")]
        [InlineData(null, null)]
        public void FirstLowerCaseWorksAsExpected(string input, string expected)
        {
            var model = new FirstLowerCaseValueFormModel();
            string actual = model.Process(null, input);
            Assert.Equal(expected, actual);
        }
    }
}
