// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

public static class DotNetTemplateExtensions
{
    public static string GetName(this DotNetTemplate template) => Enum.GetName(template)?.ToLowerInvariant() ?? throw new NotSupportedException();

    public static bool IsAspNetCore(this DotNetTemplate template) =>
        template == DotNetTemplate.Web
        || template == DotNetTemplate.Mvc
        || template == DotNetTemplate.WebApi
        || template == DotNetTemplate.Razor
        || template == DotNetTemplate.BlazorWasm
        || template == DotNetTemplate.Worker;
}
