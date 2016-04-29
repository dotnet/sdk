using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mutant.Chicken.Runner;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mutant.Chicken.Orchestrator.RunnableProjects
{
    public class RunnableProjectOrchestrator : Runner.Orchestrator
    {
        protected override IGlobalRunSpec RunSpecLoader(Stream runSpec)
        {
            return null;
        }
    }
}
