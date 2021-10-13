// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    public class DefaultLowerSafeNamespaceValueFormModelTests
    {
        [Theory]
        [InlineData("", "")]
        [InlineData("Ⅻ〇˙–⿻𠀀𠀁𪛕𪛖", "ⅻ〇_______")]
        [InlineData("𒁊𒁫¶ĚΘঊਇ", "___ěθঊਇ")]
        [InlineData("9heLLo", "_hello")]
        [InlineData("broken-clock32", "broken_clock32")]
        [InlineData(";MyWord;", "_myword_")]
        [InlineData("&&*", "___")]
        [InlineData("ÇağrışımÖrüntüsü", "çağrışımörüntüsü")]
        [InlineData("number of sockets", "number_of_sockets")]
        [InlineData("НоваяПеременная", "новаяпеременная")]
        [InlineData("Company.Product", "company.product")]
        public void LowerSafeNamespaceWorksAsExpected(string input, string expected)
        {
            var model = new DefaultLowerSafeNamespaceValueFormModel();
            string actual = model.Process(null, input);
            Assert.Equal(expected, actual);
        }
    }
}
