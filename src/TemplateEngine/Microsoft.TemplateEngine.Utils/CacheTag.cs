using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public class CacheTag : ICacheTag
    {
        public CacheTag(string description, IReadOnlyDictionary<string, string> choicesAndDescriptions, string defaultValue)
        {
            Description = description;
            ChoicesAndDescriptions = choicesAndDescriptions.CloneIfDifferentComparer(StringComparer.OrdinalIgnoreCase);
            DefaultValue = defaultValue;
        }

        public string Description { get; }

        public IReadOnlyDictionary<string, string> ChoicesAndDescriptions { get; }

        public string DefaultValue { get; }
    }
}
