// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    [TestClass]
    public class FirstUpperCaseInvariantValueFormTests
    {
        [TestMethod]
        [DataRow("a", "A", null)]
        [DataRow("no", "No", null)]
        [DataRow("new", "New", null)]
        [DataRow("", "", null)]
        [DataRow("indigo", "Indigo", "tr-TR")]
        [DataRow("ındigo", "ındigo", "tr-TR")]
        public void FirstUpperCaseInvariantWorksAsExpected(string input, string expected, string? culture)
        {
            if (!string.IsNullOrEmpty(culture))
            {
                CultureInfo.CurrentCulture = culture == "invariant" ? CultureInfo.InvariantCulture : new CultureInfo(culture);
            }
            IValueForm model = new FirstUpperCaseInvariantValueFormFactory().Create("test");
            string actual = model.Process(input, new Dictionary<string, IValueForm>());
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void CanHandleNullValue()
        {
            IValueForm model = new FirstUpperCaseInvariantValueFormFactory().Create("test");
            Assert.ThrowsExactly<ArgumentNullException>(() => model.Process(null!, new Dictionary<string, IValueForm>()));
        }
    }
}
