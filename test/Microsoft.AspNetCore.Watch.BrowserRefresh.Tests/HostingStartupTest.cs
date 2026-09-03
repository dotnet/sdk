// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh;

[TestClass]
public class HostingStartupTest
{
    [TestMethod]
    public void ConfigureServices_RegistersBrowserToolsServices()
    {
        var services = new ServiceCollection();

        new HostingStartup().ConfigureServices(services, new Uri("http://127.0.0.1:5000"));

        var tagHelperDescriptors = services
            .Where(static descriptor => descriptor.ServiceType == typeof(ITagHelperComponent))
            .ToArray();
        Assert.HasCount(1, tagHelperDescriptors);
        Assert.AreEqual(typeof(BrowserRefreshTagHelperComponent), tagHelperDescriptors[0].ImplementationType);
        Assert.Contains(
            static descriptor => descriptor.ServiceType == typeof(BrowserToolsForwarder),
            services);
    }
}
