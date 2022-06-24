// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Components;

namespace Microsoft.TemplateEngine.Edge
{
    /// <summary>
    /// The component allows to bind environment variables.
    /// </summary>
    public class EnvironmentVariablesBindSource : IBindSymbolSource
    {
        int IPrioritizedComponent.Priority => 0;

        string? IBindSymbolSource.SourcePrefix => "env";

        Guid IIdentifiedComponent.Id => Guid.Parse("{8420EB0D-2FD7-49A7-966D-0914C86A14E4}");

        string IBindSymbolSource.DisplayName => LocalizableStrings.EnvironmentVariablesBindSource_Name;

        Task<string?> IBindSymbolSource.GetBoundValueAsync(IEngineEnvironmentSettings settings, string bindname, CancellationToken cancellationToken)
        {
            return Task.FromResult(settings.Environment.GetEnvironmentVariable(bindname));
        }
    }
}
