// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Reflection;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class ReleaseLibraryInitializerTests
{
    private HttpClient? GetLibraryHttpClient()
    {
        var utilsType = Type.GetType("Microsoft.Deployment.DotNet.Releases.Utils, Microsoft.Deployment.DotNet.Releases");
        if (utilsType == null)
        {
            return null;
        }

        var httpClientField = utilsType.GetField("s_httpClient", BindingFlags.Static | BindingFlags.NonPublic);
        if (httpClientField == null)
        {
            return null;
        }

        return httpClientField.GetValue(null) as HttpClient;
    }

    [Fact]
    public void InitializeSetsDifferentUserAgentThanDnup()
    {
        // Act - Initialize the library with custom user-agent
        // This is safe to call multiple times and will only execute once
        ReleaseLibraryInitializer.Initialize();

        // Assert - Verify the library's HttpClient is available and has a user-agent
        var httpClient = GetLibraryHttpClient();
        httpClient.Should().NotBeNull("HttpClient should be initialized by the library");

        var userAgent = httpClient!.DefaultRequestHeaders.UserAgent.ToString();
        userAgent.Should().NotBeNullOrEmpty("User-agent should be set after initialization");
        userAgent.Should().Contain("dotnetup-library", "User-agent should identify library calls");
        userAgent.Should().NotContain("dotnetup-dotnet-installer", "Library user-agent should differ from dnup's direct HTTP calls");
    }

    [Fact]
    public void InitializeCanBeCalledMultipleTimes()
    {
        // Act - Call initialize multiple times (this is safe due to the lock and _initialized flag)
        ReleaseLibraryInitializer.Initialize();
        ReleaseLibraryInitializer.Initialize();
        ReleaseLibraryInitializer.Initialize();

        // Assert - No exception should be thrown and user-agent should still be set
        var httpClient = GetLibraryHttpClient();
        httpClient.Should().NotBeNull("HttpClient should be initialized by the library");

        var userAgent = httpClient!.DefaultRequestHeaders.UserAgent.ToString();
        userAgent.Should().Contain("dotnetup-library", "User-agent should remain set after multiple initializations");
    }

    [Fact]
    public void LibraryUserAgentIncludesVersion()
    {
        // Arrange
        var informationalVersion = typeof(ReleaseLibraryInitializer).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // Act - Initialize (this is idempotent)
        ReleaseLibraryInitializer.Initialize();

        // Assert
        var httpClient = GetLibraryHttpClient();
        httpClient.Should().NotBeNull("HttpClient should be initialized by the library");

        var userAgent = httpClient!.DefaultRequestHeaders.UserAgent.ToString();

        if (informationalVersion != null)
        {
            userAgent.Should().Contain(informationalVersion, "User-agent should include assembly version");
        }
        else
        {
            userAgent.Should().Be("dotnetup-library", "User-agent should be 'dotnetup-library' when no version is available");
        }
    }
}
