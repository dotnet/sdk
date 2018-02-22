using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public class CacheTag : ICacheTag, IAllowDefaultIfOptionWithoutValue
    {
        public CacheTag(string description, IReadOnlyDictionary<string, string> choicesAndDescriptions, string defaultValue)
            : this(description, choicesAndDescriptions, defaultValue, null)
        {
        }

        public CacheTag(string description, IReadOnlyDictionary<string, string> choicesAndDescriptions, string defaultValue, string defaultIfOptionWithoutValue)
        {
            Description = description;
            ChoicesAndDescriptions = choicesAndDescriptions.CloneIfDifferentComparer(StringComparer.OrdinalIgnoreCase);
            DefaultValue = defaultValue;
            DefaultIfOptionWithoutValue = defaultIfOptionWithoutValue;
        }

        public string Description { get; }

        public IReadOnlyDictionary<string, string> ChoicesAndDescriptions { get; }

        public string DefaultValue { get; }

        public string DefaultIfOptionWithoutValue { get; set; }

        public bool ShouldSerializeDefaultIfOptionWithoutValue()
        {
            return !string.IsNullOrEmpty(DefaultIfOptionWithoutValue);
        }
    }
}
