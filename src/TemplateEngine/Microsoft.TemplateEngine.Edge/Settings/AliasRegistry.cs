using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class AliasRegistry
    {
        private AliasModel _aliases { get; set; }

        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly Paths _paths;

        public AliasRegistry(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
            _paths = new Paths(environmentSettings);
        }

        public IReadOnlyDictionary<string, string> AllAliases
        {
            get
            {
                EnsureLoaded();
                return new Dictionary<string, string>(_aliases.CommandAliases, StringComparer.OrdinalIgnoreCase);
            }
        }

        private void EnsureLoaded()
        {
            if (_aliases != null)
            {
                return;
            }

            if (!_paths.Exists(_paths.User.AliasesFile))
            {
                _aliases = new AliasModel();
                return;
            }

            string sourcesText = _paths.ReadAllText(_paths.User.AliasesFile, "{}");
            JObject parsed = JObject.Parse(sourcesText);
            Dictionary<string, string> commandAliases = new Dictionary<string, string>();

            foreach (JProperty entry in parsed.PropertiesOf(nameof(_aliases.CommandAliases)))
            {
                commandAliases.Add(entry.Name, entry.Value.ToString());
            }

            _aliases = new AliasModel(commandAliases);
        }

        private void Save()
        {
            JObject serialized = JObject.FromObject(_aliases);
            _environmentSettings.Host.FileSystem.WriteAllText(_paths.User.AliasesFile, serialized.ToString());
        }

        public AliasManipulationResult TryCreateOrRemoveAlias(IReadOnlyList<string> inputTokens)
        {
            EnsureLoaded();

            IList<string> aliasTokens = new List<string>();
            bool nextIsAliasName = false;
            string aliasName = null;

            foreach (string token in inputTokens)
            {
                if (nextIsAliasName)
                {
                    aliasName = token;
                    nextIsAliasName = false;
                }
                else if (string.Equals(token, "-a", StringComparison.Ordinal) || string.Equals(token, "--alias", StringComparison.Ordinal))
                {
                    if (!string.IsNullOrEmpty(aliasName))
                    {
                        return new AliasManipulationResult(AliasManipulationStatus.InvalidInput);
                    }
                    nextIsAliasName = true;
                }
                else if (!string.Equals(token, "--debug:attach", StringComparison.Ordinal))
                {
                    aliasTokens.Add(token);
                }
            }

            if (aliasName == null)
            {
                // the input was malformed. Alias flag without alias name
                return new AliasManipulationResult(AliasManipulationStatus.InvalidInput);
            }
            else if (aliasTokens.Count == 0)
            {   // the command was just "--alias <alias name>"
                // remove the alias
                if (_aliases.TryRemoveCommandAlias(aliasName, out string removedAliasValue))
                {
                    Save();
                }
                return new AliasManipulationResult(AliasManipulationStatus.Removed, aliasName, removedAliasValue);
            }

            string aliasValue = string.Join(" ", aliasTokens);
            Dictionary<string, string> aliasesWithCandidate = new Dictionary<string, string>(_aliases.CommandAliases);
            aliasesWithCandidate[aliasName] = aliasValue;
            if (!TryExpandCommandAliases(aliasesWithCandidate, aliasValue, out string expandedInput))
            {
                // TODO: more meaningful return info... alias would create a cycle
                return new AliasManipulationResult(AliasManipulationStatus.WouldCreateCycle, aliasName, aliasValue);
            }

            _aliases.AddCommandAlias(aliasName, aliasValue);
            Save();
            return new AliasManipulationResult(AliasManipulationStatus.Created, aliasName, aliasValue);
        }

        // Attempts to expand aliases on the input string, using the aliases in _aliases
        public bool TryExpandCommandAliases(IReadOnlyList<string> inputTokens, out IReadOnlyList<string> expandedInputTokens)
        {
            EnsureLoaded();

            if (inputTokens.Count == 0)
            {
                expandedInputTokens = inputTokens;
                return true;
            }

            string input = string.Join(" ", inputTokens);
            if (TryExpandCommandAliases(_aliases.CommandAliases, input, out string expandedInput))
            {
                expandedInputTokens = expandedInput.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                return true;
            }

            // TryExpandCommandAliases() returned false because was an expansion error
            expandedInputTokens = new List<string>();
            return false;
        }

        // Attempts to perform alias expansion on the input string, using the dict of aliases passed in.
        // TODO: consider making this be all token-based. Then just the expanded aliases would need to be tokenized as we go along.
        private static bool TryExpandCommandAliases(IReadOnlyDictionary<string, string> aliases, string input, out string expandedInput)
        {
            bool expansionOccurred = false;
            expandedInput = input;
            HashSet<string> seenAliases = new HashSet<string>();

            do
            {
                string[] parts = expandedInput.Split((char[])null, 2, StringSplitOptions.RemoveEmptyEntries);
                string candidateAlias = parts[0];
                string remainingInput;
                if (parts.Length == 2)
                {
                    remainingInput = parts[1];
                }
                else
                {
                    remainingInput = string.Empty;
                }

                if (aliases.TryGetValue(candidateAlias, out string aliasExpansion))
                {
                    if (!seenAliases.Add(candidateAlias))
                    {
                        // a cycle has occurred... not allowed.
                        expandedInput = null;
                        return false;
                    }

                    expandedInput = aliasExpansion + " " + remainingInput;
                    expansionOccurred = true;
                }
                else
                {
                    expansionOccurred = false;
                }
            } while (expansionOccurred);

            return true;
        }
    }
}
