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
    /// The component allows custom host parameters.
    /// </summary>
    public sealed class HostParametersBindSource : IBindSymbolSource
    {
        int IPrioritizedComponent.Priority => 100;

        string? IBindSymbolSource.SourcePrefix => "host";

        bool IBindSymbolSource.RequiresPrefixMatch => false;

        Guid IIdentifiedComponent.Id => Guid.Parse("{63AB8956-DBFA-4DA4-8089-93CC8272D7C5}");

        string IBindSymbolSource.DisplayName => LocalizableStrings.HostParametersBindSource_Name;

        Task<string?> IBindSymbolSource.GetBoundValueAsync(IEngineEnvironmentSettings settings, string bindName, CancellationToken cancellationToken)
        {
            settings.Host.Logger.LogDebug(
        "[{0}]: Retrieving bound value for '{1}'.",
        nameof(HostParametersBindSource),
        bindName);

            settings.Host.TryGetHostParamDefault(bindName, out string? newValue);

            settings.Host.Logger.LogDebug(
        "[{0}]: Retrieved bound value for '{1}': '{2}'.",
        nameof(HostParametersBindSource),
        bindName,
        newValue ?? "<null>");

            return Task.FromResult(newValue);
        }
    }
}
