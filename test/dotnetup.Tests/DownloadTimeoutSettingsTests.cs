// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation.Internal;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[Collection("DotnetupEnvironmentMutationTests")]
public class DownloadTimeoutSettingsTests : IDisposable
{
    private readonly string? _originalIdleTimeout;
    private readonly string? _originalTotalTimeout;

    public DownloadTimeoutSettingsTests()
    {
        _originalIdleTimeout = Environment.GetEnvironmentVariable(DownloadTimeoutSettings.IdleTimeoutSecondsEnvVar);
        _originalTotalTimeout = Environment.GetEnvironmentVariable(DownloadTimeoutSettings.TotalTimeoutSecondsEnvVar);
        Environment.SetEnvironmentVariable(DownloadTimeoutSettings.IdleTimeoutSecondsEnvVar, null);
        Environment.SetEnvironmentVariable(DownloadTimeoutSettings.TotalTimeoutSecondsEnvVar, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(DownloadTimeoutSettings.IdleTimeoutSecondsEnvVar, _originalIdleTimeout);
        Environment.SetEnvironmentVariable(DownloadTimeoutSettings.TotalTimeoutSecondsEnvVar, _originalTotalTimeout);
    }

    [Fact]
    public void FromEnvironment_UsesDefaultTimeoutsWhenUnset()
    {
        var settings = DownloadTimeoutSettings.FromEnvironment();

        settings.IdleTimeout.Should().Be(TimeSpan.FromMinutes(2));
        settings.TotalTimeout.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void FromEnvironment_UsesPositiveSecondOverrides()
    {
        Environment.SetEnvironmentVariable(DownloadTimeoutSettings.IdleTimeoutSecondsEnvVar, "7");
        Environment.SetEnvironmentVariable(DownloadTimeoutSettings.TotalTimeoutSecondsEnvVar, "11");

        var settings = DownloadTimeoutSettings.FromEnvironment();

        settings.IdleTimeout.Should().Be(TimeSpan.FromSeconds(7));
        settings.TotalTimeout.Should().Be(TimeSpan.FromSeconds(11));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public void FromEnvironment_IgnoresInvalidOverrides(string value)
    {
        Environment.SetEnvironmentVariable(DownloadTimeoutSettings.IdleTimeoutSecondsEnvVar, value);
        Environment.SetEnvironmentVariable(DownloadTimeoutSettings.TotalTimeoutSecondsEnvVar, value);

        var settings = DownloadTimeoutSettings.FromEnvironment();

        settings.IdleTimeout.Should().Be(TimeSpan.FromMinutes(2));
        settings.TotalTimeout.Should().Be(TimeSpan.FromMinutes(15));
    }
}