// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Components;

namespace Microsoft.TemplateEngine.Edge
{
    /// <summary>
    /// The component allows custom host parameters.
    /// </summary>
    public class HostParametersBindSource : IBindSymbolSource
    {
        int IPrioritizedComponent.Priority => 100;

        string? IBindSymbolSource.SourcePrefix => "host";

        Guid IIdentifiedComponent.Id => Guid.Parse("{63AB8956-DBFA-4DA4-8089-93CC8272D7C5}");

        string IBindSymbolSource.DisplayName => LocalizableStrings.HostParametersBindSource_Name;

        Task<string?> IBindSymbolSource.GetBoundValueAsync(IEngineEnvironmentSettings settings, string bindname, CancellationToken cancellationToken)
        {
            settings.Host.TryGetHostParamDefault(bindname, out string? newValue);
            return Task.FromResult(newValue);
        }
    }
}
