// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal interface IValueFormFactory
    {
        /// <summary>
        /// Gets form identifier.
        /// </summary>
        string Identifier { get; }

        /// <summary>
        /// Creates the form with default configuration.
        /// </summary>
        IValueForm Create(string? name = null);

        /// <summary>
        /// Creates the form from JSON configuration.
        /// </summary>
        IValueForm FromJObject(string name, JObject? configuration = null);
    }
}
