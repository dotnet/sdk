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

        // The order corresponds to the order of the instructions in the same action
        // in the culture neutral TemplateConfigFile
        IReadOnlyList<string> Instructions { get; }
    }
}
