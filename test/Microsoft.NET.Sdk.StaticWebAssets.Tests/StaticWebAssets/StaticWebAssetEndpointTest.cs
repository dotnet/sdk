// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

public class StaticWebAssetEndpointTest
{
    [Theory]
    [InlineData("App1/css/app.css", "App1")]
    [InlineData("App1", "App1")]
    [InlineData("App1/css/styles/app.css", "App1/css")]
    [InlineData("App1/css/app.css", "")]
    [InlineData("", "")]
    [InlineData("App1\\css\\app.css", "App1")]
    [InlineData("App1/css\\app.css", "App1")]
    [InlineData("App1/App1.lib.module.js", "App1")]
    [InlineData("app1/css/app.css", "App1")]
    [InlineData("APP1/css/app.css", "app1")]
    public void RouteHasPathPrefix_ReturnsTrue_WhenRouteStartsWithPrefixAsPathSegment(string route, string prefix)
    {
        var routeSegments = new List<PathTokenizer.Segment>();
        var prefixSegments = new List<PathTokenizer.Segment>();

        var result = StaticWebAssetEndpoint.RouteHasPathPrefix(route, prefix, routeSegments, prefixSegments);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("App1.styles.css", "App1")]
    [InlineData("App1", "App1/css/app.css")]
    [InlineData("App1/js/app.js", "App1/css")]
    [InlineData("App12/css/app.css", "App1")]
    [InlineData("App1Bundle/app.js", "App1")]
    [InlineData("App1.lib.module.js", "App1")]
    public void RouteHasPathPrefix_ReturnsFalse_WhenRouteDoesNotStartWithPrefixAsPathSegment(string route, string prefix)
    {
        var routeSegments = new List<PathTokenizer.Segment>();
        var prefixSegments = new List<PathTokenizer.Segment>();

        var result = StaticWebAssetEndpoint.RouteHasPathPrefix(route, prefix, routeSegments, prefixSegments);

        result.Should().BeFalse();
    }

    [Fact]
    public void RouteHasPathPrefix_ReusesSegmentLists()
    {
        var routeSegments = new List<PathTokenizer.Segment>();
        var prefixSegments = new List<PathTokenizer.Segment>();

        StaticWebAssetEndpoint.RouteHasPathPrefix("a/b/c", "a", routeSegments, prefixSegments);
        StaticWebAssetEndpoint.RouteHasPathPrefix("x/y/z", "x/y", routeSegments, prefixSegments);

        var result = StaticWebAssetEndpoint.RouteHasPathPrefix("App1/css/app.css", "App1", routeSegments, prefixSegments);

        result.Should().BeTrue();
    }
}
