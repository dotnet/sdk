// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Utils
{
#pragma warning disable CS0618 // Type or member is obsolete - compatibility
    public class TemplateParameter : ITemplateParameter, IAllowDefaultIfOptionWithoutValue
#pragma warning restore CS0618 // Type or member is obsolete
    {
        /// <summary>
        /// Deserialization constructor.
        /// </summary>
        /// <param name="jObject"></param>
        public TemplateParameter(JObject jObject)
        {
            string? name = jObject.ToString(nameof(Name));
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"{nameof(Name)} property should not be null or whitespace", nameof(jObject));
            }

            Name = name!;
            Type = jObject.ToString(nameof(Type)) ?? "parameter";
            DataType = jObject.ToString(nameof(DataType)) ?? "string";
            Description = jObject.ToString(nameof(Description));

            int priority = jObject.ToInt32(nameof(Priority));
            Priority = Enum.IsDefined(typeof(TemplateParameterPriority), priority) ? (TemplateParameterPriority)priority : default;

            Priority = (TemplateParameterPriority)jObject.ToInt32(nameof(Priority));

            DefaultValue = jObject.ToString(nameof(DefaultValue));
            DefaultIfOptionWithoutValue = jObject.ToString(nameof(DefaultIfOptionWithoutValue));
            DisplayName = jObject.ToString(nameof(DisplayName));
            IsName = jObject.ToBool(nameof(IsName));

            if (this.IsChoice())
            {
                Type = "parameter";
                Dictionary<string, ParameterChoice> choices = new Dictionary<string, ParameterChoice>(StringComparer.OrdinalIgnoreCase);
                JObject? cdToken = jObject.Get<JObject>(nameof(Choices));
                if (cdToken != null)
                {
                    foreach (JProperty cdPair in cdToken.Properties())
                    {
                        choices.Add(
                            cdPair.Name.ToString(),
                            new ParameterChoice(
                                cdPair.Value.ToString(nameof(ParameterChoice.DisplayName)),
                                cdPair.Value.ToString(nameof(ParameterChoice.Description))));
                    }
                }
                Choices = choices;
            }

        }

        public TemplateParameter(
            string name,
            string type,
            string datatype,
            TemplateParameterPriority priority = default,
            bool isName = false,
            string? defaultValue = null,
            string? defaultIfOptionWithoutValue = null,
            string? description = null,
            string? displayName = null,
            IReadOnlyDictionary<string, ParameterChoice>? choices = null)
        {
            Name = name;
            Type = type;
            DataType = datatype;
            Priority = priority;
            IsName = isName;
            DefaultValue = defaultValue;
            DefaultIfOptionWithoutValue = defaultIfOptionWithoutValue;
            Description = description;
            DisplayName = displayName;

            if (this.IsChoice())
            {
                Choices = choices ?? new Dictionary<string, ParameterChoice>();
            }
        }

        [Obsolete("Use Description instead.")]
        public string? Documentation => Description;

        [JsonProperty]
        public string Name { get; }

        [JsonProperty]
        public TemplateParameterPriority Priority { get; }

        [JsonProperty]
        public string Type { get; }

        [JsonProperty]
        public bool IsName { get; }

        [JsonProperty]
        public string? DefaultValue { get; }

        [JsonProperty]
        public string DataType { get; set; }

        [JsonProperty]
        public string? DefaultIfOptionWithoutValue { get; set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, ParameterChoice>? Choices { get; }

        [JsonProperty]
        public string? Description { get; }

        [JsonProperty]
        public string? DisplayName { get; }
    }

}
