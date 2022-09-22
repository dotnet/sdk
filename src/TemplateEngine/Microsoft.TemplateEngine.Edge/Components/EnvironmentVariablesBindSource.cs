// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Components;

namespace Microsoft.TemplateEngine.Edge
{
    /// <summary>
    /// The component allows to bind environment variables.
    /// </summary>
    public sealed class EnvironmentVariablesBindSource : IBindSymbolSource
    {
        int IPrioritizedComponent.Priority => 0;

        string? IBindSymbolSource.SourcePrefix => "env";

        bool IBindSymbolSource.RequiresPrefixMatch => false;

        Guid IIdentifiedComponent.Id => Guid.Parse("{8420EB0D-2FD7-49A7-966D-0914C86A14E4}");

        string IBindSymbolSource.DisplayName => LocalizableStrings.EnvironmentVariablesBindSource_Name;

        Task<string?> IBindSymbolSource.GetBoundValueAsync(IEngineEnvironmentSettings settings, string bindname, CancellationToken cancellationToken)
        {
            settings.Host.Logger.LogDebug(
                "[{0}]: Retrieving bound value for '{1}'.",
                nameof(EnvironmentVariablesBindSource),
                bindname);

            string? result = settings.Environment.GetEnvironmentVariable(bindname);

            settings.Host.Logger.LogDebug(
        "[{0}]: Retrieved bound value for '{1}': '{2}'.",
        nameof(EnvironmentVariablesBindSource),
        bindname,
        result ?? "<null>");
            return Task.FromResult(result);
        }
    }
}
