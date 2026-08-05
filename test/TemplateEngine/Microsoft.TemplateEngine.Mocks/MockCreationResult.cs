// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockCreationResult : ICreationResult
    {
        public MockCreationResult(IReadOnlyList<IPostAction>? postActions = null, IReadOnlyList<ICreationPath>? primaryOutputs = null)
        {
            PostActions = postActions ?? [];
            PrimaryOutputs = primaryOutputs ?? [];
        }

        public IReadOnlyList<IPostAction> PostActions { get; }

        public IReadOnlyList<ICreationPath> PrimaryOutputs { get; }
    }
}
