using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class AliasRegistry
    {
        private JObject _source;
        private readonly Dictionary<string, string> AliasesToTemplates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> TemplatesToAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly Paths _paths;

        public AliasRegistry(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
            _paths = new Paths(environmentSettings);
        }

        private void Load()
        {
            if (TemplatesToAliases.Count > 0)
            {
                return;
            }

            if (!_paths.Exists(_paths.User.AliasesFile))
            {
                _source = new JObject();
                return;
            }

            string sourcesText = _paths.ReadAllText(_paths.User.AliasesFile, "{}");
            _source = JObject.Parse(sourcesText);

            foreach (JProperty child in _source.Properties())
            {
                AliasesToTemplates[child.Name] = child.Value.ToString();
                TemplatesToAliases[child.Value.ToString()] = child.Name;
            }
        }

        public string GetTemplateNameForAlias(string alias)
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

        public IReadOnlyDictionary<string, ITemplateInfo> GetTemplatesForAlias(string alias, IReadOnlyCollection<ITemplateInfo> templates)
        {
            Dictionary<string, ITemplateInfo> aliasVsTemplate = new Dictionary<string, ITemplateInfo>();

            if(alias == null)
            {
                return aliasVsTemplate;
            }

            Load();
            string templateName;
            if(AliasesToTemplates.TryGetValue(alias, out templateName))
            {
                ITemplateInfo match = templates.FirstOrDefault(x => string.Equals(x.Name, templateName, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    aliasVsTemplate[alias] = match;
                    return aliasVsTemplate;
                }
            }

            if (!string.IsNullOrWhiteSpace(alias))
            {
                Dictionary<string, string> matchedAliases = AliasesToTemplates.Where(x => x.Key.IndexOf(alias, StringComparison.OrdinalIgnoreCase) > -1).ToDictionary(x => x.Value, x => x.Key);

                foreach (ITemplateInfo template in templates)
                {
                    if (matchedAliases.TryGetValue(template.Name, out string matchingAlias))
                    {
                        aliasVsTemplate[matchingAlias] = template;
                    }
                }
            }

            return aliasVsTemplate;
        }

        public string GetAliasForTemplate(ITemplateInfo template)
        {
            Load();
            string alias;
            if(!TemplatesToAliases.TryGetValue(template.Name, out alias))
            {
                return null;
            }

            return alias;
        }

        // returns -1 if the alias already exists, zero otherwise
        public int SetTemplateAlias(string alias, ITemplateInfo template)
        {
            Load();
            if (AliasesToTemplates.ContainsKey(alias))
            {
                return -1;
            }

            _source[alias] = template.Name;
            _environmentSettings.Host.FileSystem.WriteAllText(_paths.User.AliasesFile, _source.ToString());
            return 0;
        }
    }
}
