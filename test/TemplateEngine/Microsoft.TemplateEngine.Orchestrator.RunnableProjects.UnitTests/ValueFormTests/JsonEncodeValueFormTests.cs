using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    public class JsonEncodeValueFormTests
    {
        [Theory]
        [InlineData("asdf\"asdf", "\"asdf\\\"asdf\"")]
        [InlineData("asdfasdf", "\"asdfasdf\"")]
        public void JsonEncodingWorksAsExpected(string input, string expected)
        {
            JsonEncodeValueFormModel model = new JsonEncodeValueFormModel();
            string actual = model.Process(null, input);
            Assert.Equal(expected, actual);
        }
    }
}
