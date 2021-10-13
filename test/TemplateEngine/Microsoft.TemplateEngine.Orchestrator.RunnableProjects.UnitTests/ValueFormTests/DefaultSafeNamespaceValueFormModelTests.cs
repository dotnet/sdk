// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    public class DefaultSafeNamespaceValueFormModelTests
    {
        [Theory]
        [InlineData("", "")]
        [InlineData("Ⅻ〇˙–⿻𠀀𠀁𪛕𪛖", "Ⅻ〇_______")]
        [InlineData("𒁊𒁫¶ĚΘঊਇ", "___ĚΘঊਇ")]
        [InlineData("9heLLo", "_heLLo")]
        [InlineData("broken-clock32", "broken_clock32")]
        [InlineData(";MyWord;", "_MyWord_")]
        [InlineData("&&*", "___")]
        [InlineData("ÇağrışımÖrüntüsü", "ÇağrışımÖrüntüsü")]
        [InlineData("number of sockets", "number_of_sockets")]
        [InlineData("НоваяПеременная", "НоваяПеременная")]
        [InlineData("Company.Product", "Company.Product")]
        [InlineData("b913671e-9e12-4ed6-a350-3c44e6b34502", "b913671e.9e12.4ed6.a350.3c44e6b34502")]
        public void SafeNamespaceWorksAsExpected(string input, string expected)
        {
            var model = new DefaultSafeNamespaceValueFormModel();
            string actual = model.Process(null, input);
            Assert.Equal(expected, actual);
        }
    }
}
