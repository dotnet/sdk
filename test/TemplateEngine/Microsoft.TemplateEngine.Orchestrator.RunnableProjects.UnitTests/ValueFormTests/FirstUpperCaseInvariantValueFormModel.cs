using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using System.Globalization;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    public class FirstUpperCaseInvariantValueFormTests
    {
        [Theory]
        [InlineData("a", "A", null)]
        [InlineData("no", "No", null)]
        [InlineData("new", "New", null)]
        [InlineData("", "", null)]
        [InlineData(null, null, null)]
        [InlineData("indigo", "Indigo", "tr-TR")]
        [InlineData("ındigo", "ındigo", "tr-TR")]
        public void FirstUpperCaseInvariantWorksAsExpected(string input, string expected, string culture)
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
            var model = new FirstUpperCaseInvariantValueFormModel();
            string actual = model.Process(null, input);
            Assert.Equal(expected, actual);
        }
    }
}
