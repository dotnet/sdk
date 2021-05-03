// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IPostAction
    {
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
        /// Gets the instructions that should be manually performed by the user
        /// as part of this post action.
        /// Manual instructions are used when the host does not have the associated post action processor implemented.
        /// </summary>
        string? ManualInstructions { get; }

        string? ConfigFile { get; }
    }
}
