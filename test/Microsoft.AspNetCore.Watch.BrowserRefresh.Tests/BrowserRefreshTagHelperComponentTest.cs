// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh;

[TestClass]
public class BrowserRefreshTagHelperComponentTest
{
    [TestMethod]
    public async Task ProcessAsync_AppendsBrowserRefreshScriptToBody()
    {
        var component = new BrowserRefreshTagHelperComponent();
        var output = CreateOutput("body");

        await component.ProcessAsync(CreateContext("body"), output);

        Assert.AreEqual(int.MaxValue, component.Order);
        Assert.AreEqual(
            "<script type=\"module\" src=\"/_framework/dotnet-browser-tools/browser-tools-bootstrap.js\"></script>",
            output.PostContent.GetContent());
    }

    [TestMethod]
    public async Task ProcessAsync_DoesNotModifyOtherTags()
    {
        var component = new BrowserRefreshTagHelperComponent();
        var output = CreateOutput("head");

        await component.ProcessAsync(CreateContext("head"), output);

        Assert.AreEqual(string.Empty, output.PostContent.GetContent());
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
