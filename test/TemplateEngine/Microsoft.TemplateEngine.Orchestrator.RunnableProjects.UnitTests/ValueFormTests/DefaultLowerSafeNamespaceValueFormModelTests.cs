// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    [TestClass]
    public class DefaultLowerSafeNamespaceValueFormModelTests
    {
        [TestMethod]
        [DataRow("", "")]
        [DataRow("Ⅻ〇˙–⿻𠀀𠀁𪛕𪛖", "ⅻ〇_______")]
        [DataRow("𒁊𒁫¶ĚΘঊਇ", "___ěθঊਇ")]
        [DataRow("9heLLo", "_9hello")]
        [DataRow("broken-clock32", "broken_clock32")]
        [DataRow(";MyWord;", "_myword_")]
        [DataRow("&&*", "___")]
        [DataRow("ÇağrışımÖrüntüsü", "çağrışımörüntüsü")]
        [DataRow("number of sockets", "number_of_sockets")]
        [DataRow("НоваяПеременная", "новаяпеременная")]
        [DataRow("Company.Product", "company.product")]
        public void LowerSafeNamespaceWorksAsExpected(string input, string expected)
        {
            IValueForm model = new DefaultLowerSafeNamespaceValueFormFactory().Create("test");
            string actual = model.Process(input, new Dictionary<string, IValueForm>());
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void CanHandleNullValue()
        {
            IValueForm model = new DefaultLowerSafeNamespaceValueFormFactory().Create("test");
            Assert.ThrowsExactly<ArgumentNullException>(() => model.Process(null!, new Dictionary<string, IValueForm>()));
        }
    }
}
