// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh;

internal sealed class BrowserRefreshTagHelperComponent : ITagHelperComponent
{
    public int Order => int.MaxValue;

    public void Init(TagHelperContext context)
    {
    }

    public Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (string.Equals(context.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            output.PostContent.AppendHtml(ScriptInjectingStream.InjectedScript);
        }

        return Task.CompletedTask;
    }
}
