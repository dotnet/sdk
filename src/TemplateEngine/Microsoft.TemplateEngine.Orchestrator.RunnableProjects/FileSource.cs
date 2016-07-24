using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class FileSource
    {
        public IReadOnlyList<string> Include { get; set; }

        public IReadOnlyList<string> Exclude { get; set; }

        public IReadOnlyDictionary<string, string> Rename { get; set; }

        public string Source { get; set; }

        public string Target { get; set; }

        public IReadOnlyList<string> CopyOnly { get; set; }
    }
}