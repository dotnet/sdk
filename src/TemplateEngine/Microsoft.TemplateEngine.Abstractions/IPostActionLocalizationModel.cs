// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IPostActionLocalizationModel
    {
        Guid ActionId { get; }

        string? Description { get; }

        /// <summary>
        /// Gets the localized manual instructions that the user should perform.
        /// The key represents the index of the instruction as it appears in the
        /// same post action in the template config file.
        /// </summary>
        IReadOnlyDictionary<int, string> Instructions { get; }
    }
}
