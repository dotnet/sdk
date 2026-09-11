// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class ContainerRuntimeOperationsTests
{
    [TestMethod]
    public async Task LoadFromStandardInputAsync_drains_process_output_while_writing()
    {
        string command = OperatingSystem.IsWindows() ? "findstr" : "cat";
        string[] arguments = OperatingSystem.IsWindows() ? [".*"] : [];
        byte[] input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("container image data\n", 100_000)));
        var operations = new ContainerRuntimeOperations(
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            (_, _, _) => Task.FromResult(true));

        await operations.LoadFromStandardInputAsync(
            command,
            arguments,
            input,
            default,
            default,
            static (bytes, _, _, stream, cancellationToken) => stream.WriteAsync(bytes, cancellationToken).AsTask(),
            TestContext.CancellationToken);
    }

    public TestContext TestContext { get; set; } = default!;
}
