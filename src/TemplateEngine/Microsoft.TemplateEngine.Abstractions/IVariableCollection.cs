// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Defines an interface for generic variable collection.
    /// The variable collection may have a parent.
    /// The parent collection is used when the value is not found in current collection.
    /// </summary>
    public interface IVariableCollection : IDictionary<string, object>
    {
        /// <summary>
        /// Gets the parent collection for the instance.
        /// </summary>
        IVariableCollection? Parent { get; set; }
    }
}
