using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Runner;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class RunnableProjectOrchestrator : Runner.Orchestrator
    {
        protected override IGlobalRunSpec RunSpecLoader(Stream runSpec)
        {
            return null;
        }
    }
}
