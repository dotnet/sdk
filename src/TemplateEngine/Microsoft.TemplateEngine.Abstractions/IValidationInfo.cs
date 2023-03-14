// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IValidationInfo
    {
        /// <summary>
        /// <see langword="true"/> when the template is valid to be used.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Gets the results of template validation.
        /// </summary>
        IReadOnlyList<IValidationEntry> ValidationErrors { get; }
    }
}
