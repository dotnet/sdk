using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using System.Globalization;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    public class FirstLowerCaseInvariantValueFormTests
    {
        [Theory]
        [InlineData("A", "a", null)]
        [InlineData("NO", "nO", null)]
        [InlineData("NEW", "nEW", null)]
        [InlineData("", "", null)]
        [InlineData(null, null, null)]
        [InlineData("Indigo", "indigo", "tr-TR")]
        [InlineData("İndigo", "İndigo", "tr-TR")]
        public void FirstLowerCaseInvariantWorksAsExpected(string input, string expected, string culture)
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

            var model = new FirstLowerCaseInvariantValueFormModel();
            string actual = model.Process(null, input);
            Assert.Equal(expected, actual);
        }
    }
}
