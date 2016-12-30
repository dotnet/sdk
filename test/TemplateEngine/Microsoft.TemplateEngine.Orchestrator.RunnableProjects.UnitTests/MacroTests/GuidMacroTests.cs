using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;


namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class GuidMacroTests : TestBase
    {
        [Fact(DisplayName = nameof(TestDeferredConfig))]
        public void TestDeferredConfig()
        {
            IMacroConfig config = new GuidMacroConfig("myGuid1", null);

            IParameterSet parameters = new RunnableProjectGenerator.ParameterSet(null);
        }
    }
}
