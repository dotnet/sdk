// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    // Stores info about the template creation, to be returned to the host
    public interface ICreationResult
    {
        IReadOnlyList<IPostAction> PostActions { get; }

        IReadOnlyList<ICreationPath> PrimaryOutputs { get; }
    }
}
