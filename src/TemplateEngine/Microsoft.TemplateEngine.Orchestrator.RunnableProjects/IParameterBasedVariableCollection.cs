// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Parameters;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    /// <summary>
    /// Extends the <see cref="IVariableCollection"/> contract with the metadata
    /// about data from template parameters.
    /// </summary>
    internal interface IParameterBasedVariableCollection : IVariableCollection
    {
        /// <summary>
        /// Bound and merged parameter data and their metadata.
        /// </summary>
        IParameterSetData ParameterSetData { get; }
    }
}
