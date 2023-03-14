// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.Serialization
{
    internal static class Extensions
    {
        /// <summary>
        /// Serializes model to JSON string.
        /// Note that not all members of <see cref="TemplateConfigModel"/> are supported.
        /// For details, see <see cref="TemplateConfigModelJsonConverter"/>.
        /// </summary>
        /// <exception cref="NotSupportedException">when attempting to serialize model which contains unsupported member.</exception>
        internal static string ToJsonString(this TemplateConfigModel templateConfigModel)
            => JsonConvert.SerializeObject(templateConfigModel, Formatting.Indented, TemplateConfigModelJsonConverter.Instance, ParameterSymbolJsonConverter.Instance);
    }
}
