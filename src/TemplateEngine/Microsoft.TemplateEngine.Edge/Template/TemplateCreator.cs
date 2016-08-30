using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.Template
{
    public static class TemplateCreator
    {
        public static IReadOnlyCollection<ITemplateInfo> List(string searchString)
        {
            HashSet<ITemplateInfo> matchingTemplates = new HashSet<ITemplateInfo>(TemplateEqualityComparer.Default);
            HashSet<ITemplateInfo> allTemplates = new HashSet<ITemplateInfo>(TemplateEqualityComparer.Default);

            using (Timing.Over("load"))
                SettingsLoader.GetTemplates(allTemplates);

            using (Timing.Over("Search in loaded"))
                foreach (ITemplateInfo template in allTemplates)
                {
                    if (string.IsNullOrEmpty(searchString)
                        || template.Name.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) > -1
                        || template.ShortName.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        matchingTemplates.Add(template);
                    }
                }

            using (Timing.Over("Alias search"))
            {
#if !NET451
                IReadOnlyCollection<ITemplateInfo> allTemplatesCollection = allTemplates;
#else
                IReadOnlyCollection<ITemplateInfo> allTemplatesCollection = allTemplates.ToList();
#endif
                matchingTemplates.UnionWith(AliasRegistry.GetTemplatesForAlias(searchString, allTemplatesCollection));
            }

#if !NET451
            IReadOnlyCollection<ITemplateInfo> matchingTemplatesCollection = matchingTemplates;
#else
            IReadOnlyCollection<ITemplateInfo> matchingTemplatesCollection = matchingTemplates.ToList();
#endif
            return matchingTemplatesCollection;
        }

        public static bool TryGetTemplate(string templateName, out ITemplateInfo tmplt)
        {
            try
            {
                using (Timing.Over("List"))
                {
                    IReadOnlyCollection<ITemplateInfo> result = List(templateName);

                    if (result.Count == 1)
                    {
                        tmplt = result.First();
                        return true;
                    }
                }
            }
            catch
            {
            }

            tmplt = null;
            return false;
        }
    }
}
