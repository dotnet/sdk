// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.TemplateEngine.Utils
{
    /// <summary>
    /// Defines version specification used in template definition.
    /// </summary>
    public interface IVersionSpecification
    {
        /// <summary>
        /// Checks is version is valid.
        /// </summary>
        /// <param name="versionToCheck">the version to check.</param>
        /// <returns></returns>
        bool CheckIfVersionIsValid(string versionToCheck);
    }
}
