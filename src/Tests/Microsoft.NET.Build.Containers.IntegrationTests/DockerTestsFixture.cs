// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.TestFramework;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public sealed class DockerTestsFixture : IDisposable
{
    private readonly SharedTestOutputHelper _diagnosticOutput;

    public DockerTestsFixture(IMessageSink messageSink)
    {
        _diagnosticOutput = new SharedTestOutputHelper(messageSink);
        try
        {
            DockerRegistryManager.StartAndPopulateDockerRegistry(_diagnosticOutput);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            DockerRegistryManager.ShutdownDockerRegistry(_diagnosticOutput);
        }
        catch
        {
            _diagnosticOutput.WriteLine("Failed to shutdown docker registry, shut down it manually");
        }
    }
}
