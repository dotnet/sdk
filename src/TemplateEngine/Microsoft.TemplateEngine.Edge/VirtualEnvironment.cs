// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge
{
    /// <summary>
    /// Virtualized implementation of <see cref="IEnvironment"/>.
    /// Allows to overwrite and/or add environment variables not defined in physical environment.
    /// </summary>
    public class VirtualEnvironment : DefaultEnvironment
    {
        /// <summary>
        /// Creates new instance of <see cref="VirtualEnvironment"/>.
        /// </summary>
        /// <param name="virtualEnvironment">Variables to be considered as environment variables. They have precedence over physical environment variables.</param>
        /// <param name="includeRealEnvironment">If set to true - variables from <see cref="Environment"/> are added.</param>
        public VirtualEnvironment(IReadOnlyDictionary<string, string>? virtualEnvironment, bool includeRealEnvironment)
            : base(MergeEnvironmentVariables(virtualEnvironment, includeRealEnvironment))
        { }

        private static IReadOnlyDictionary<string, string> MergeEnvironmentVariables(
            IReadOnlyDictionary<string, string>? virtualEnvironment, bool includeRealEnvironment)
        {
            Dictionary<string, string> variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (includeRealEnvironment)
            {
                variables.Merge(FetchEnvironmentVariables());
            }

            if (virtualEnvironment != null)
            {
                variables.Merge(virtualEnvironment);
            }

            return variables;
        }
    }
}
