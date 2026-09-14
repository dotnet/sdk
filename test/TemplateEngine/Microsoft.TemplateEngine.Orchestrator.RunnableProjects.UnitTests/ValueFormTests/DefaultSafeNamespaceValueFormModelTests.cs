// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    [TestClass]
    public class DefaultSafeNamespaceValueFormModelTests
    {
        [TestMethod]
        [DataRow("", "")]
        [DataRow("Ⅻ〇˙–⿻𠀀𠀁𪛕𪛖", "Ⅻ〇_______")]
        [DataRow("𒁊𒁫¶ĚΘঊਇ", "___ĚΘঊਇ")]
        [DataRow("9heLLo", "_9heLLo")]
        [DataRow("broken-clock32", "broken_clock32")]
        [DataRow(";MyWord;", "_MyWord_")]
        [DataRow("&&*", "___")]
        [DataRow("ÇağrışımÖrüntüsü", "ÇağrışımÖrüntüsü")]
        [DataRow("number of sockets", "number_of_sockets")]
        [DataRow("НоваяПеременная", "НоваяПеременная")]
        [DataRow("Company.Product", "Company.Product")]
        [DataRow("b913671e-9e12-4ed6-a350-3c44e6b34502", "b913671e_9e12_4ed6_a350_3c44e6b34502")]
        [DataRow("1Template.1", "_1Template._1")]
        [DataRow("Template.1", "Template._1")]
        public void SafeNamespaceWorksAsExpected(string input, string expected)
        {
            IValueForm model = new DefaultSafeNamespaceValueFormFactory().Create("test");
            string actual = model.Process(input, new Dictionary<string, IValueForm>());
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void CanHandleNullValue()
        {
            IValueForm model = new DefaultSafeNamespaceValueFormFactory().Create("test");
            Assert.ThrowsExactly<ArgumentNullException>(() => model.Process(null!, new Dictionary<string, IValueForm>()));
        }
    }
}
