using System;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class LocalizationLocator : ILocalizationLocator
    {
        public string Locale { get; set; }

        public Guid MountPointId { get; set; }

        public string ConfigPlace { get; set; }

        public string Identity { get; set; }
    }
}
