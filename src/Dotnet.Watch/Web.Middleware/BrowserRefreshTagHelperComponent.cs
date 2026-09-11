// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Razor.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh;

/// <summary>
/// Appends the application hosted browser tools configuration module to MVC and Razor Pages
/// responses. The module and the client it imports are part of the application's build output, so
/// the browser never executes code obtained from the provider it authenticates.
/// </summary>
internal sealed class BrowserRefreshTagHelperComponent(IHttpContextAccessor httpContextAccessor) : ITagHelperComponent
{
    public int Order => int.MaxValue;

    public void Init(TagHelperContext context)
    {
    }

    public Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (string.Equals(context.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            // Static web assets are served under the application's path base.
            var pathBase = httpContextAccessor.HttpContext?.Request.PathBase ?? default;
            output.PostContent.AppendHtml($"<script type=\"module\" src=\"{pathBase + ApplicationPaths.BrowserToolsConfigJS}\"></script>");
        }

        return Task.CompletedTask;
    }
}
