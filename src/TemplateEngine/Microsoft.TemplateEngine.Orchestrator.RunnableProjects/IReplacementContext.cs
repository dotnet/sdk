// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal interface IReplacementContext
    {
        string OnlyIfBefore { get; }

        string OnlyIfAfter { get; }
    }
}