// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockCreationResult : ICreationResult
    {
        public IReadOnlyList<IPostAction> PostActions { get; set; }

        public IReadOnlyList<ICreationPath> PrimaryOutputs { get; set; }
    }
}
