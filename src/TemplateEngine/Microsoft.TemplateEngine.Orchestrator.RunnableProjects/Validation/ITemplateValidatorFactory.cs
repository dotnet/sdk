// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation
{
    [Flags]
    internal enum ValidationScope
    {
        None = 0,

        /// <summary>
        /// The check is run when scanning the mount point for templates, typically installation or scanning scenario.
        /// This set is more exhaustive then <see cref="Instantiation"/>, and should be used to avoid installing invalid templates.
        /// </summary>
        Scanning = 1,

        /// <summary>
        /// The check is run when the template is being instantiated.
        /// This set should be normally less exhaustive then <see cref="Scanning"/>, and should be used to check for failures that may affect the template instantiation result.
        /// </summary>
        Instantiation = 2,
    }

    internal interface ITemplateValidatorFactory : IIdentifiedComponent
    {
        ValidationScope Scope { get; }

        Task<ITemplateValidator> CreateValidatorAsync(IEngineEnvironmentSettings engineEnvironmentSettings, CancellationToken cancellationToken);
    }
}
