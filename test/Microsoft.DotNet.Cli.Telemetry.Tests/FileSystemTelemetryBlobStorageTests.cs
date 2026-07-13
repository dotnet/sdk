// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Telemetry.Implementation;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Microsoft.DotNet.Cli.Telemetry.Tests;

[TestClass]
public class FileSystemTelemetryBlobStorageTests
{
    private static string NewNonexistentDirectory()
        => Path.Combine(Path.GetTempPath(), "cli-telemetry-tests", Guid.NewGuid().ToString("N"));

    [TestMethod]
    public void ConstructingStorageDoesNotCreateTheStorageDirectory()
    {
        var directory = NewNonexistentDirectory();

        _ = new FileSystemTelemetryBlobStorage(directory);

        // FileBlobProvider creates the directory in its constructor, so the storage must defer
        // building it. Merely constructing the storage (which happens when the exporter is
        // registered, even under telemetry opt-out) must not touch the file system.
        Directory.Exists(directory).Should().BeFalse("constructing storage must not create the directory");
    }

    [TestMethod]
    public void RegisteringTheExporterDoesNotCreateTheStorageDirectory()
    {
        var directory = NewNonexistentDirectory();

        // Registration runs the same code path as TelemetryClient's static constructor, which
        // executes regardless of the per-invocation opt-out check. It must not create ~/.dotnet.
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddPersistentStorageExporter(o =>
            {
                o.ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://example.contoso.com/";
                o.StorageDirectory = directory;
            })
            .Build();

        Directory.Exists(directory).Should().BeFalse("registering the exporter must not create the storage directory");
    }

    [TestMethod]
    public void PersistingCreatesTheStorageDirectoryOnFirstUse()
    {
        var directory = NewNonexistentDirectory();
        try
        {
            var storage = new FileSystemTelemetryBlobStorage(directory);

            storage.TryPersist([1, 2, 3]).Should().BeTrue();

            // The directory is created lazily, only when telemetry actually flows.
            Directory.Exists(directory).Should().BeTrue("persisting telemetry must create the storage directory");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
