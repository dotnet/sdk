// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IPostActionLocalizationModel
    {
        Guid ActionId { get; }

        string Description { get; }

        /// <summary>
        /// Gets the localized manual instructions that the user should perform.
        /// The order of the items in this list are the same as the order of the
        /// instructions in the same post action in the template.
        /// </summary>
        IReadOnlyList<string> Instructions { get; }
    }
}
