// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.TemplateEngine.Utils
{
    /// <summary>
    /// Defines a version specification used in a template definition.
    /// </summary>
    public interface IVersionSpecification
    {
        /// <summary>
        /// Checks if the specified version is valid.
        /// </summary>
        /// <param name="versionToCheck">the version to check.</param>
        /// <returns></returns>
        bool CheckIfVersionIsValid(string versionToCheck);
    }
}
