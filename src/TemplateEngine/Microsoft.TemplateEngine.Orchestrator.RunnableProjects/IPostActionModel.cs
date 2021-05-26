// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal interface IPostActionModel : IConditionedConfigurationElement
    {
        /// <summary>
        /// Gets a string that uniquely identifies this post action within a template.
        /// </summary>
        string? Id { get; }

        /// <summary>
        /// Gets the description of the post action.
        /// </summary>
        string? Description { get; }

        /// <summary>
        /// Gets the identifier of the action that will be performed.
        /// Note that this is not an identifier for the post action itself.
        /// </summary>
        Guid ActionId { get; }

        /// <summary>
        /// Gets a value indicating wheather the template instantiation should continue
        /// in case of an error with this post action.
        /// </summary>
        bool ContinueOnError { get; }

        /// <summary>
        /// Gets the arguments for this post action.
        /// </summary>
        IReadOnlyDictionary<string, string> Args { get; }

        /// <summary>
        /// Gets the list of instructions that should be manually performed by the user.
        /// "instruction" contains the text that explains the steps to be taken by the user.
        /// An instruction is only considered if the "condition" evaluates to true.
        /// </summary>
        IReadOnlyList<ManualInstructionModel> ManualInstructionInfo { get; }
    }
}
