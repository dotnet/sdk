// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Reflection;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class ReleaseLibraryInitializerTests
{
    [Fact]
    public void InitializeSetsDifferentUserAgentThanDnup()
    {
        // Arrange - Get the library's HttpClient before initialization
        var utilsType = Type.GetType("Microsoft.Deployment.DotNet.Releases.Utils, Microsoft.Deployment.DotNet.Releases");
        utilsType.Should().NotBeNull("Microsoft.Deployment.DotNet.Releases.Utils type should be available");

        var httpClientField = utilsType!.GetField("s_httpClient", BindingFlags.Static | BindingFlags.NonPublic);
        httpClientField.Should().NotBeNull("s_httpClient field should exist");

        var httpClient = httpClientField!.GetValue(null) as HttpClient;
        httpClient.Should().NotBeNull("HttpClient should be initialized by the library");

        // Act - Initialize the library with custom user-agent
        ReleaseLibraryInitializer.Initialize();

        // Assert - Verify the user-agent is set to identify library calls
        var userAgent = httpClient!.DefaultRequestHeaders.UserAgent.ToString();
        userAgent.Should().NotBeNullOrEmpty("User-agent should be set after initialization");
        userAgent.Should().Contain("dotnetup-library", "User-agent should identify library calls");
        userAgent.Should().NotContain("dotnetup-dotnet-installer", "Library user-agent should differ from dnup's direct HTTP calls");
    }

    [Fact]
    public void InitializeCanBeCalledMultipleTimes()
    {
        // Act - Call initialize multiple times
        ReleaseLibraryInitializer.Initialize();
        ReleaseLibraryInitializer.Initialize();
        ReleaseLibraryInitializer.Initialize();

        // Assert - No exception should be thrown
        var utilsType = Type.GetType("Microsoft.Deployment.DotNet.Releases.Utils, Microsoft.Deployment.DotNet.Releases");
        var httpClientField = utilsType!.GetField("s_httpClient", BindingFlags.Static | BindingFlags.NonPublic);
        var httpClient = httpClientField!.GetValue(null) as HttpClient;
        var userAgent = httpClient!.DefaultRequestHeaders.UserAgent.ToString();

        userAgent.Should().Contain("dotnetup-library", "User-agent should remain set after multiple initializations");
    }

    [Fact]
    public void LibraryUserAgentIncludesVersion()
    {
        // Arrange
        var informationalVersion = typeof(ReleaseLibraryInitializer).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // Act
        ReleaseLibraryInitializer.Initialize();

        // Assert
        var utilsType = Type.GetType("Microsoft.Deployment.DotNet.Releases.Utils, Microsoft.Deployment.DotNet.Releases");
        var httpClientField = utilsType!.GetField("s_httpClient", BindingFlags.Static | BindingFlags.NonPublic);
        var httpClient = httpClientField!.GetValue(null) as HttpClient;
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
