// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    /// <summary>
    /// Defines an interface for value form.
    /// </summary>
    public interface IValueForm
    {
        /// <summary>
        /// Gets value form identifier.
        /// Identifier determines the transformation to be run.
        /// </summary>
        string Identifier { get; }

        /// <summary>
        /// Gets value form name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Transforms <paramref name="value"/>.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="otherForms">other forms defined in the template.</param>
        /// <returns>transformed value.</returns>
        string Process(string value, IReadOnlyDictionary<string, IValueForm> otherForms);
    }
}
