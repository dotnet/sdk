// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    /// <summary>
    /// Represents an instruction that should be manually performed by the user
    /// as part of a post action.
    /// </summary>
    internal sealed class ManualInstructionModel : ConditionedConfigurationElementBase
    {
        public ManualInstructionModel(string text)
        {
            Text = text;
        }

        public ManualInstructionModel(string text, string? condition)
        {
            Text = text;
            Condition = condition;
        }

        /// <summary>
        /// Gets the text explaining the steps the user should take.
        /// </summary>
        public string Text { get; }
    }
}
