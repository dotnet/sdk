// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using Microsoft.DotNet.Cli.Commands.Test.IPC;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class IPCTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SingleConnectionNamedPipeServer_MultipleConnection_Fails()
    {
        var cancellationToken = TestContext.CancellationToken;
        string pipeName = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));

        List<NamedPipeServer> openedPipes = [];
        List<Exception> exceptions = [];

        ManualResetEventSlim waitException = new(false);
        var waitTask = Task.Run(
            async () =>
            {
                try
                {
                    while (true)
                    {
                        var singleConnectionNamedPipeServer = new NamedPipeServer(
                            pipeName,
                            (_, _) => Task.FromResult<IResponse>(VoidResponse.CachedInstance),
                            maxNumberOfServerInstances: 1,
                            cancellationToken,
                            skipUnknownMessages: false);

                        await singleConnectionNamedPipeServer.WaitConnectionAsync(cancellationToken);
                        openedPipes.Add(singleConnectionNamedPipeServer);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    waitException.Set();
                }
            }, cancellationToken);

        var namedPipeClient1 = new NamedPipeClient(pipeName);
        await namedPipeClient1.ConnectAsync(cancellationToken);
        waitException.Wait(cancellationToken);

        var openedPipe = Assert.ContainsSingle(openedPipes);
        var exception = Assert.ContainsSingle(exceptions);
        Assert.AreEqual(typeof(IOException), exception.GetType());
        Assert.Contains("All pipe instances are busy.", exception.Message);

        await waitTask;
        namedPipeClient1.Dispose();
        openedPipe.Dispose();

        // Verify double dispose
        namedPipeClient1.Dispose();
        openedPipe.Dispose();
    }

    // CAREFUL: This test produces random test cases.
    // So, flakiness in this test might be an indicator to a serious product bug.
    [TestMethod]
    public async Task SingleConnectionNamedPipeServer_RequestReplySerialization_Succeeded()
    {
        var cancellationToken = TestContext.CancellationToken;
        const long longValue = (long)int.MaxValue + 1;
        Queue<BaseMessage> receivedMessages = new();
        string pipeName = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
        NamedPipeClient namedPipeClient = new(pipeName);
        namedPipeClient.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        namedPipeClient.RegisterSerializer(new TextMessageSerializer(), typeof(TextMessage));
        namedPipeClient.RegisterSerializer(new IntMessageSerializer(), typeof(IntMessage));
        namedPipeClient.RegisterSerializer(new LongMessageSerializer(), typeof(LongMessage));

        ManualResetEventSlim manualResetEventSlim = new(false);
        var clientConnected = Task.Run(
            async () =>
            {
                while (true)
                {
                    try
                    {
                        await namedPipeClient.ConnectAsync(cancellationToken);
                        manualResetEventSlim.Set();
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        throw new OperationCanceledException("SingleConnectionNamedPipeServer_RequestReplySerialization_Succeeded cancellation during connect");
                    }
                    catch (Exception)
                    {
                    }
                }
            }, cancellationToken);
        NamedPipeServer singleConnectionNamedPipeServer = new(
            pipeName,
            (_, request) =>
            {
                receivedMessages.Enqueue((BaseMessage)request);
                return Task.FromResult<IResponse>(VoidResponse.CachedInstance);
            },
            NamedPipeServerStream.MaxAllowedServerInstances,
            cancellationToken,
            skipUnknownMessages: false);
        singleConnectionNamedPipeServer.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        singleConnectionNamedPipeServer.RegisterSerializer(new TextMessageSerializer(), typeof(TextMessage));
        singleConnectionNamedPipeServer.RegisterSerializer(new IntMessageSerializer(), typeof(IntMessage));
        singleConnectionNamedPipeServer.RegisterSerializer(new LongMessageSerializer(), typeof(LongMessage));
        await singleConnectionNamedPipeServer.WaitConnectionAsync(cancellationToken);
        manualResetEventSlim.Wait(cancellationToken);

        await clientConnected;

        await namedPipeClient.RequestReplyAsync<IntMessage, VoidResponse>(new IntMessage(10), cancellationToken);
        Assert.AreEqual(new IntMessage(10), receivedMessages.Dequeue());

        await namedPipeClient.RequestReplyAsync<LongMessage, VoidResponse>(new LongMessage(longValue), cancellationToken);
        Assert.AreEqual(new LongMessage(longValue), receivedMessages.Dequeue());

        for (int i = 0; i < 100; i++)
        {
            await AssertWithLengthAsync(Random.Shared.Next(1024, 1024 * 1024 * 2));
        }

        // NOTE: 250000 is the buffer size of NamedPipeServer.
        // We explicitly test around this size (and multiple of it) as most potential bugs can be around it.
        for (int multiple = 1; multiple <= 3; multiple++)
        {
            const int namedPipeServerBufferSize = 250000;
            int minLength = namedPipeServerBufferSize * multiple - 1000;
            int maxLength = namedPipeServerBufferSize * multiple + 1000;
            for (int randomLength = minLength; randomLength <= maxLength; randomLength++)
            {
                await AssertWithLengthAsync(randomLength);
            }
        }

        namedPipeClient.Dispose();
        singleConnectionNamedPipeServer.Dispose();

        async Task AssertWithLengthAsync(int length)
        {
            string currentString = RandomString(length);
            await namedPipeClient.RequestReplyAsync<TextMessage, VoidResponse>(new TextMessage(currentString), cancellationToken);
            Assert.ContainsSingle(receivedMessages);
            Assert.AreEqual(new TextMessage(currentString), receivedMessages.Dequeue());
        }
    }

    [TestMethod]
    public async Task NamedPipeServer_ReadRequestAsync_ReadsOnlyOneFrame()
    {
        NamedPipeServer server = CreateServer();
        try
        {
            server.RegisterSerializer(new IntMessageSerializer(), typeof(IntMessage));

            byte[] firstFrame = CreateFrame(new IntMessageSerializer().Id, BitConverter.GetBytes(1));
            byte[] secondFrame = CreateFrame(new IntMessageSerializer().Id, BitConverter.GetBytes(2));
            using var stream = new MemoryStream([.. firstFrame, .. secondFrame]);

            Assert.AreEqual(new IntMessage(1), await server.ReadRequestAsync(stream, TestContext.CancellationToken));
            Assert.AreEqual(firstFrame.Length, stream.Position);
            Assert.AreEqual(new IntMessage(2), await server.ReadRequestAsync(stream, TestContext.CancellationToken));
            Assert.AreEqual(stream.Length, stream.Position);
        }
        finally
        {
            server.Dispose();
        }
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(sizeof(int) - 1)]
    public async Task NamedPipeServer_ReadRequestAsync_RejectsMalformedPayloadLength(int payloadLength)
    {
        NamedPipeServer server = CreateServer();
        try
        {
            using var stream = new MemoryStream(BitConverter.GetBytes(payloadLength));

            InvalidDataException exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(
                async () => await server.ReadRequestAsync(stream, TestContext.CancellationToken));

            Assert.Contains("payload length", exception.Message);
        }
        finally
        {
            server.Dispose();
        }
    }

    [TestMethod]
    public async Task NamedPipeServer_ReadRequestAsync_RejectsOversizedFrame()
    {
        NamedPipeServer server = CreateServer();
        try
        {
            using var stream = new MemoryStream(BitConverter.GetBytes(NamedPipeServer.MaximumFrameSize));

            InvalidDataException exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(
                async () => await server.ReadRequestAsync(stream, TestContext.CancellationToken));

            Assert.Contains(NamedPipeServer.MaximumFrameSize.ToString(), exception.Message);
        }
        finally
        {
            server.Dispose();
        }
    }

    [TestMethod]
    public async Task NamedPipeServer_ReadRequestAsync_TruncatedFrameReturnsNull()
    {
        NamedPipeServer server = CreateServer();
        try
        {
            server.RegisterSerializer(new IntMessageSerializer(), typeof(IntMessage));
            byte[] frame = CreateFrame(serializerId: 3, BitConverter.GetBytes(1));
            using var stream = new MemoryStream(frame, 0, frame.Length - 1);

            IRequest? request = await server.ReadRequestAsync(stream, TestContext.CancellationToken);

            Assert.IsNull(request);

            using var completeStream = new MemoryStream(CreateFrame(serializerId: 3, BitConverter.GetBytes(2)));
            Assert.AreEqual(new IntMessage(2), await server.ReadRequestAsync(completeStream, TestContext.CancellationToken));
        }
        finally
        {
            server.Dispose();
        }
    }

    [TestMethod]
    public void ResolvePipeDirectory_UsesExplicitOverride()
    {
        Assert.AreEqual(("override", true), NamedPipeServer.ResolvePipeDirectory("override", "temp"));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void ResolvePipeDirectory_UsesTempPathWhenOverrideIsMissing(string? overrideDirectory)
    {
        Assert.AreEqual(("temp", false), NamedPipeServer.ResolvePipeDirectory(overrideDirectory, "temp"));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void ResolvePipeDirectory_FallsBackToTmpWhenTempPathIsMissing(string? tempPath)
    {
        Assert.AreEqual(("/tmp", false), NamedPipeServer.ResolvePipeDirectory(null, tempPath));
    }

    [TestMethod]
    public void GetUnixPipePath_NormalizesToAbsolutePath()
    {
        string root = Path.GetPathRoot(Environment.CurrentDirectory)!;
        string directory = Path.Combine(root, "mtp", "..", "pipes");

        string pipePath = NamedPipeServer.GetUnixPipePath("pipe", directory);

        Assert.AreEqual(Path.Combine(Path.GetFullPath(directory), "pipe"), pipePath);
        Assert.IsTrue(Path.IsPathFullyQualified(pipePath));
    }

    [TestMethod]
    public void EnsurePathLengthWithinLimit_UsesUtf8ByteLength()
    {
        NamedPipeServer.EnsurePathLengthWithinLimit(new string('a', NamedPipeServer.MaxUnixDomainSocketPathLengthInBytes));

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => NamedPipeServer.EnsurePathLengthWithinLimit(new string('\u00E9', 52)));

        Assert.Contains(NamedPipeServer.PipeDirectoryEnvironmentVariable, exception.Message);
        Assert.Contains(NamedPipeServer.MaxUnixDomainSocketPathLengthInBytes.ToString(), exception.Message);
    }

    [TestMethod]
    public void EnsureDirectoryIsWritable_CreatesMissingDirectory()
    {
        string directory = Path.Combine(TestContext.TestRunDirectory!, $"mtp-pipes-{Guid.NewGuid():N}");
        try
        {
            NamedPipeServer.EnsureDirectoryIsWritable(directory);

            Assert.IsTrue(Directory.Exists(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EnsureDirectoryIsWritable_RejectsFilePath()
    {
        string filePath = Path.Combine(TestContext.TestRunDirectory!, $"mtp-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(filePath, "");
        try
        {
            string invalidDirectory = Path.Combine(filePath, "subdirectory");
            InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
                () => NamedPipeServer.EnsureDirectoryIsWritable(invalidDirectory));

            Assert.Contains(invalidDirectory, exception.Message);
            Assert.Contains(NamedPipeServer.PipeDirectoryEnvironmentVariable, exception.Message);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [TestMethod]
    public async Task ConnectionNamedPipeServer_MultipleConnection_Succeeds()
    {
        var cancellationToken = TestContext.CancellationToken;
        string pipeName = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));

        List<NamedPipeServer> pipes = [];
        for (int i = 0; i < 3; i++)
        {
            pipes.Add(new NamedPipeServer(
                pipeName,
                (_, _) => Task.FromResult<IResponse>(VoidResponse.CachedInstance),
                maxNumberOfServerInstances: 3,
                cancellationToken,
                skipUnknownMessages: false));
        }

        IOException exception = Assert.ThrowsExactly<IOException>(() =>
             new NamedPipeServer(
                pipeName,
                (_, _) => Task.FromResult<IResponse>(VoidResponse.CachedInstance),
                maxNumberOfServerInstances: 3,
                cancellationToken,
                skipUnknownMessages: false));
        Assert.Contains("All pipe instances are busy.", exception.Message);

        List<Task> waitConnectionTask = [];
        int connectionCompleted = 0;
        foreach (NamedPipeServer namedPipeServer in pipes)
        {
            waitConnectionTask.Add(Task.Run(
                async () =>
                {
                    await namedPipeServer.WaitConnectionAsync(cancellationToken);
                    Interlocked.Increment(ref connectionCompleted);
                }, cancellationToken));
        }

        List<NamedPipeClient> connectedClients = [];
        for (int i = 0; i < waitConnectionTask.Count; i++)
        {
            var namedPipeClient = new NamedPipeClient(pipeName);
            connectedClients.Add(namedPipeClient);
            await namedPipeClient.ConnectAsync(cancellationToken);
        }

        await Task.WhenAll([.. waitConnectionTask]);

        Assert.AreEqual(3, connectionCompleted);

        foreach (NamedPipeClient namedPipeClient in connectedClients)
        {
            namedPipeClient.Dispose();
        }

        foreach (NamedPipeServer namedPipeServer in pipes)
        {
            namedPipeServer.Dispose();
        }
    }

    private NamedPipeServer CreateServer()
        => new(
            NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N")),
            (_, _) => Task.FromResult<IResponse>(VoidResponse.CachedInstance),
            NamedPipeServerStream.MaxAllowedServerInstances,
            TestContext.CancellationToken,
            skipUnknownMessages: false);

    private static byte[] CreateFrame(int serializerId, byte[] body)
    {
        int payloadLength = sizeof(int) + body.Length;
        byte[] frame = new byte[sizeof(int) + payloadLength];
        BitConverter.TryWriteBytes(frame.AsSpan(0, sizeof(int)), payloadLength);
        BitConverter.TryWriteBytes(frame.AsSpan(sizeof(int), sizeof(int)), serializerId);
        body.CopyTo(frame.AsSpan(sizeof(int) * 2));
        return frame;
    }

    private static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string([.. Enumerable.Repeat(chars, length).Select(s => s[Random.Shared.Next(s.Length)])]);
    }

    private abstract record BaseMessage : IRequest;

    private sealed record TextMessage(string Text) : BaseMessage;

    private sealed class TextMessageSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => 2;

        public object Deserialize(Stream stream) => new TextMessage(ReadString(stream));

        public void Serialize(object objectToSerialize, Stream stream) => WriteString(stream, ((TextMessage)objectToSerialize).Text);
    }

    private sealed record IntMessage(int Integer) : BaseMessage;

    private sealed class IntMessageSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => 3;

        public object Deserialize(Stream stream) => new IntMessage(ReadInt(stream));

        public void Serialize(object objectToSerialize, Stream stream) => WriteInt(stream, ((IntMessage)objectToSerialize).Integer);
    }

    private sealed record LongMessage(long Long) : BaseMessage;

    private sealed class LongMessageSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => 4;

        public object Deserialize(Stream stream) => new LongMessage(ReadLong(stream));

        public void Serialize(object objectToSerialize, Stream stream) => WriteLong(stream, ((LongMessage)objectToSerialize).Long);
    }

}
