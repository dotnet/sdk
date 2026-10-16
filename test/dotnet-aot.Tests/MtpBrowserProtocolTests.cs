// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http.Headers;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.IPC;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
public class MtpBrowserProtocolTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TransferCodecAndFilesystemCommitWorkInTheNativeClosure()
    {
        var serializer = new ProtocolMessageSerializer();
        serializer.RegisterAllSerializers();
        var request = new FileArtifactTransferRequest("execution", "instance", "transfer", "artifact.txt", 3, 0, [1, 2, 3], true);
        var decoded = Assert.IsInstanceOfType<FileArtifactTransferRequest>(
            serializer.Deserialize(serializer.Serialize(request), false));
        Assert.AreSequenceEqual(request.Data, decoded.Data);
        string directory = Directory.CreateTempSubdirectory("native-browser-artifacts-").FullName;
        try
        {
            List<string> errors = [];
            using var manager = new FileArtifactTransferManager(directory, FileArtifactTransferLimits.Default, errors.Add);
            FileArtifactTransferResponse response = manager.Receive(decoded);
            Assert.IsNull(response.Error);
            Assert.IsNotNull(response.FullPath);
            Assert.AreSequenceEqual(request.Data, File.ReadAllBytes(response.FullPath));
            Assert.IsTrue(manager.OwnsPublishedPath(response.FullPath));
            Assert.IsFalse(manager.HasIncompleteOrFailedTransfers);
            Assert.AreEqual(response, serializer.Deserialize(serializer.Serialize(response), false));
            Assert.IsEmpty(errors);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task NativeHttpControlWaitDoesNotBlockProtocolTraffic()
    {
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<IResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var gateway = new HttpTestHostGateway(
            request =>
            {
                if (request is WaitForServerControlRequest)
                {
                    received.TrySetResult();
                    return completion.Task;
                }

                return Task.FromResult<IResponse>(VoidResponse.CachedInstance);
            },
            TestContext.CancellationToken);
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        var serializer = new ProtocolMessageSerializer();
        serializer.RegisterAllSerializers();
        Task<HttpResponseMessage> control = SendAsync(WaitForServerControlRequest.CachedInstance);
        try
        {
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
            using HttpResponseMessage response = await SendAsync(new HandshakeMessage([]))
                .WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsFalse(control.IsCompleted);
        }
        finally
        {
            completion.TrySetResult(new ServerControlMessage(ServerControlKinds.CancelSession));
            using HttpResponseMessage response = await control.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        }

        async Task<HttpResponseMessage> SendAsync(object message)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, gateway.Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gateway.Token);
            request.Content = new ByteArrayContent(serializer.Serialize(message));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return await client.SendAsync(request, TestContext.CancellationToken);
        }
    }
}
