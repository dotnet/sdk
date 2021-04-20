// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class ReplacementContext : IReplacementContext
    {
        internal ReplacementContext(string before, string after)
        {
            OnlyIfBefore = before;
            OnlyIfAfter = after;
        }

        public string OnlyIfBefore { get; }

        public string OnlyIfAfter { get; }
    }
}
