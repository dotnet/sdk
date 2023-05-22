// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    public class DefaultSafeNamespaceValueFormModelTests
    {
        [Theory]
        [InlineData("", "")]
        [InlineData("Ⅻ〇˙–⿻𠀀𠀁𪛕𪛖", "Ⅻ〇_______")]
        [InlineData("𒁊𒁫¶ĚΘঊਇ", "___ĚΘঊਇ")]
        [InlineData("9heLLo", "_9heLLo")]
        [InlineData("broken-clock32", "broken_clock32")]
        [InlineData(";MyWord;", "_MyWord_")]
        [InlineData("&&*", "___")]
        [InlineData("ÇağrışımÖrüntüsü", "ÇağrışımÖrüntüsü")]
        [InlineData("number of sockets", "number_of_sockets")]
        [InlineData("НоваяПеременная", "НоваяПеременная")]
        [InlineData("Company.Product", "Company.Product")]
        [InlineData("b913671e-9e12-4ed6-a350-3c44e6b34502", "b913671e_9e12_4ed6_a350_3c44e6b34502")]
        [InlineData("1Template.1", "_1Template._1")]
        [InlineData("Template.1", "Template._1")]
        public void SafeNamespaceWorksAsExpected(string input, string expected)
        {
            IValueForm model = new DefaultSafeNamespaceValueFormFactory().Create("test");
            string actual = model.Process(input, new Dictionary<string, IValueForm>());
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CanHandleNullValue()
        {
            IValueForm model = new DefaultSafeNamespaceValueFormFactory().Create("test");
            Assert.Throws<ArgumentNullException>(() => model.Process(null!, new Dictionary<string, IValueForm>()));
        }
    }
}
