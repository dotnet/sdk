using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public static class AliasRegistry
    {
        private static JObject _source;
        private static readonly Dictionary<string, string> AliasesToTemplates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> TemplatesToAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static void Load()
        {
            if (TemplatesToAliases.Count > 0)
            {
                return;
            }

            if (!Paths.User.AliasesFile.Exists())
            {
                _source = new JObject();
                return;
            }

            string sourcesText = Paths.User.AliasesFile.ReadAllText("{}");
            _source = JObject.Parse(sourcesText);

            foreach (JProperty child in _source.Properties())
            {
                AliasesToTemplates[child.Name] = child.Value.ToString();
                TemplatesToAliases[child.Value.ToString()] = child.Name;
            }
        }

        public static string GetTemplateNameForAlias(string alias)
        {
            if(alias == null)
            {
                return null;
            }

            Load();
            string templateName;
            if (AliasesToTemplates.TryGetValue(alias, out templateName))
            {
                return templateName;
            }

            return null;
        }

        public static IReadOnlyCollection<ITemplateInfo> GetTemplatesForAlias(string alias, IReadOnlyCollection<ITemplateInfo> templates)
        {
            if(alias == null)
            {
                return new ITemplate[0];
            }

            Load();
            string templateName;
            if(AliasesToTemplates.TryGetValue(alias, out templateName))
            {
                ITemplateInfo match = templates.FirstOrDefault(x => string.Equals(x.Name, templateName, StringComparison.Ordinal));

                if (match != null)
                {
                    return new[] { match };
                }
            }


            if (!string.IsNullOrWhiteSpace(alias))
            {
                HashSet<string> matchedAliases = new HashSet<string>(AliasesToTemplates.Where(x => x.Key.IndexOf(alias, StringComparison.OrdinalIgnoreCase) > -1).Select(x => x.Value));

                List<ITemplateInfo> results = new List<ITemplateInfo>();

                foreach (ITemplateInfo template in templates)
                {
                    if (matchedAliases.Contains(template.Name))
                    {
                        results.Add(template);
                    }
                }

                return results;
            }

            return templates;
        }

        public static string GetAliasForTemplate(ITemplateInfo template)
        {
            Load();
            string alias;
            if(!TemplatesToAliases.TryGetValue(template.Name, out alias))
            {
                return null;
            }

            return alias;
        }

        public static void SetTemplateAlias(string alias, ITemplateInfo template)
        {
            Load();
            _source[alias] = template.Name;
            File.WriteAllText(Paths.User.AliasesFile, _source.ToString());
        }
    }
}
