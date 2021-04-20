// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class AliasModel
    {
        public AliasModel()
            : this(new Dictionary<string, IReadOnlyList<string>>())
        {
        }

        public AliasModel(IReadOnlyDictionary<string, IReadOnlyList<string>> commandAliases)
        {
            CommandAliases = new Dictionary<string, IReadOnlyList<string>>(commandAliases.ToDictionary(x => x.Key, x => x.Value), StringComparer.OrdinalIgnoreCase);
        }

        public void AddCommandAlias(string aliasName, IReadOnlyList<string> aliasTokens)
        {
            CommandAliases.Add(aliasName, aliasTokens);
        }

        public bool TryRemoveCommandAlias(string aliasName, out IReadOnlyList<string> aliasTokens)
        {
            if (CommandAliases.TryGetValue(aliasName, out aliasTokens))
            {
                CommandAliases.Remove(aliasName);
                return true;
            }

            return false;
        }

        [JsonProperty]
        public Dictionary<string, IReadOnlyList<string>> CommandAliases { get; set; }
    }
}
