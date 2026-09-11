// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh;

[TestClass]
public class BrowserRefreshTagHelperComponentTest
{
    [TestMethod]
    public async Task ProcessAsync_AppendsApplicationHostedConfigurationModuleToBody()
    {
        var component = new BrowserRefreshTagHelperComponent(CreateAccessor(pathBase: null));
        var output = CreateOutput("body");

        await component.ProcessAsync(CreateContext("body"), output);

        Assert.AreEqual(int.MaxValue, component.Order);
        Assert.AreEqual(
            "<script type=\"module\" src=\"/_framework/Microsoft.NET.Sdk.Web.DotNetWatch.BrowserTools.Config.js\"></script>",
            output.PostContent.GetContent());
    }

    [TestMethod]
    public async Task ProcessAsync_PrefixesThePathBase()
    {
        var component = new BrowserRefreshTagHelperComponent(CreateAccessor("/app"));
        var output = CreateOutput("body");

        await component.ProcessAsync(CreateContext("body"), output);

        Assert.AreEqual(
            "<script type=\"module\" src=\"/app/_framework/Microsoft.NET.Sdk.Web.DotNetWatch.BrowserTools.Config.js\"></script>",
            output.PostContent.GetContent());
    }

    [TestMethod]
    public async Task ProcessAsync_DoesNotModifyOtherTags()
    {
        var component = new BrowserRefreshTagHelperComponent(CreateAccessor(pathBase: null));
        var output = CreateOutput("head");

        await component.ProcessAsync(CreateContext("head"), output);

        Assert.AreEqual(string.Empty, output.PostContent.GetContent());
    }

    private static IHttpContextAccessor CreateAccessor(string? pathBase)
    {
        var context = new DefaultHttpContext();
        if (pathBase != null)
        {
            context.Request.PathBase = pathBase;
        }

        return new HttpContextAccessor { HttpContext = context };
    }

    private static TagHelperContext CreateContext(string tagName)
        => new(
            tagName,
            new TagHelperAttributeList(),
            new Dictionary<object, object>(),
            uniqueId: "test");

    private static TagHelperOutput CreateOutput(string tagName)
        => new(
            tagName,
            new TagHelperAttributeList(),
            static (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
}
