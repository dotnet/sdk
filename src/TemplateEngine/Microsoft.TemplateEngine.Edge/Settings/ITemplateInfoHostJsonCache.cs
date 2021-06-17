// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    /// <summary>
    /// Extension interface of <see cref="ITemplateInfo"/> which stores .host.json data in cache so host doesn't need to re-load it from .nupkg every time.
    /// </summary>
    public interface ITemplateInfoHostJsonCache
    {
        /// <summary>
        /// Full content of .host.json file.
        /// </summary>
        JObject? HostData { get; }
    }
}
