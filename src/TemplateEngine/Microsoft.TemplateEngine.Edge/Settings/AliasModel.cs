using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class AliasModel
    {
        public AliasModel()
            : this(new Dictionary<string, string>())
        {
        }

        public AliasModel(Dictionary<string, string> commandAliases)
        {
            CommandAliases = new Dictionary<string, string>(commandAliases, StringComparer.OrdinalIgnoreCase);
        }

        public void AddCommandAlias(string aliasName, string aliasValue)
        {
            CommandAliases.Add(aliasName, aliasValue);
        }

        public bool TryRemoveCommandAlias(string aliasName, out string aliasValue)
        {
            if (CommandAliases.TryGetValue(aliasName, out aliasValue))
            {
                CommandAliases.Remove(aliasName);
                return true;
            }

            return false;
        }

        [JsonProperty]
        public Dictionary<string, string> CommandAliases { get; set; }
    }
}
