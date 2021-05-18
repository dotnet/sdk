// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
#pragma warning disable CS0618 // Type or member is obsolete
    internal class Parameter : ITemplateParameter, IAllowDefaultIfOptionWithoutValue
#pragma warning restore CS0618 // Type or member is obsolete
    {
        [JsonProperty]
        public IReadOnlyDictionary<string, ParameterChoice> Choices { get; internal set; }

        [JsonIgnore]
        public string Documentation
        {
            get { return Description; }
            internal set { Description = value; }
        }

        [JsonProperty]
        public string Description { get; internal set; }

        [JsonProperty]
        public string DefaultValue { get; internal set; }

        [JsonProperty]
        public string Name { get; internal set; }

        [JsonProperty]
        public string DisplayName { get; internal set; }

        [JsonProperty]
        public bool IsName { get; internal set; }

        [JsonProperty]
        public TemplateParameterPriority Priority { get; internal set; }

        [JsonProperty]
        public string Type { get; internal set; }

        [JsonProperty]
        public string DataType { get; internal set; }

        [JsonProperty]
        public string DefaultIfOptionWithoutValue { get; set; }

        [JsonProperty]
        internal bool IsVariable { get; set; }

        public override string ToString()
        {
            return $"{Name} ({Type})";
        }
    }
}
