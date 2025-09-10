// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using Microsoft.DotNet.Cli.Commands.Test.IPC;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

namespace dotnet.Tests.CommandTests.Test;

public class IPCTests
{
    [Fact]
    public async Task SingleConnectionNamedPipeServer_MultipleConnection_Fails()
    {
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
                            CancellationToken.None,
                            skipUnknownMessages: false);

                        await singleConnectionNamedPipeServer.WaitConnectionAsync(CancellationToken.None);
                        openedPipes.Add(singleConnectionNamedPipeServer);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    waitException.Set();
                }
            });

        var namedPipeClient1 = new NamedPipeClient(pipeName);
        await namedPipeClient1.ConnectAsync(CancellationToken.None);
        waitException.Wait();

        var openedPipe = Assert.Single(openedPipes);
        var exception = Assert.Single(exceptions);
        Assert.Equal(typeof(IOException), exception.GetType());
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
    [Fact]
    public async Task SingleConnectionNamedPipeServer_RequestReplySerialization_Succeeded()
    {
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
                        await namedPipeClient.ConnectAsync(CancellationToken.None);
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
            }, CancellationToken.None);
        NamedPipeServer singleConnectionNamedPipeServer = new(
            pipeName,
            (_, request) =>
            {
                receivedMessages.Enqueue((BaseMessage)request);
                return Task.FromResult<IResponse>(VoidResponse.CachedInstance);
            },
            NamedPipeServerStream.MaxAllowedServerInstances,
            CancellationToken.None,
            skipUnknownMessages: false);
        singleConnectionNamedPipeServer.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        singleConnectionNamedPipeServer.RegisterSerializer(new TextMessageSerializer(), typeof(TextMessage));
        singleConnectionNamedPipeServer.RegisterSerializer(new IntMessageSerializer(), typeof(IntMessage));
        singleConnectionNamedPipeServer.RegisterSerializer(new LongMessageSerializer(), typeof(LongMessage));
        await singleConnectionNamedPipeServer.WaitConnectionAsync(CancellationToken.None);
        manualResetEventSlim.Wait();

        await clientConnected;

        await namedPipeClient.RequestReplyAsync<IntMessage, VoidResponse>(new IntMessage(10), CancellationToken.None);
        Assert.Equal(new IntMessage(10), receivedMessages.Dequeue());

        await namedPipeClient.RequestReplyAsync<LongMessage, VoidResponse>(new LongMessage(11), CancellationToken.None);
        Assert.Equal(new LongMessage(11), receivedMessages.Dequeue());

        for (int i = 0; i < 100; i++)
        {
            string currentString = RandomString(Random.Shared.Next(1024, 1024 * 1024 * 2));
            await namedPipeClient.RequestReplyAsync<TextMessage, VoidResponse>(new TextMessage(currentString), CancellationToken.None);
            Assert.Single(receivedMessages);
            Assert.Equal(new TextMessage(currentString), receivedMessages.Dequeue());
        }

        // NOTE: 250000 is the buffer size of NamedPipeServer.
        // We explicitly test around this size as most potential bugs can be around it.
        for (int randomLength = 250000 - 1000; randomLength < 250000 + 1000; randomLength++)
        {
            string currentString = RandomString(randomLength);
            await namedPipeClient.RequestReplyAsync<TextMessage, VoidResponse>(new TextMessage(currentString), CancellationToken.None);
            Assert.Single(receivedMessages);
            Assert.Equal(new TextMessage(currentString), receivedMessages.Dequeue());
        }

        namedPipeClient.Dispose();
        singleConnectionNamedPipeServer.Dispose();
    }

    [Fact]
    public async Task ConnectionNamedPipeServer_MultipleConnection_Succeeds()
    {
        string pipeName = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));

        List<NamedPipeServer> pipes = [];
        for (int i = 0; i < 3; i++)
        {
            pipes.Add(new NamedPipeServer(
                pipeName,
                (_, _) => Task.FromResult<IResponse>(VoidResponse.CachedInstance),
                maxNumberOfServerInstances: 3,
                CancellationToken.None,
                skipUnknownMessages: false));
        }

        IOException exception = Assert.Throws<IOException>(() =>
             new NamedPipeServer(
                pipeName,
                (_, _) => Task.FromResult<IResponse>(VoidResponse.CachedInstance),
                maxNumberOfServerInstances: 3,
                CancellationToken.None,
                skipUnknownMessages: false));
        Assert.Contains("All pipe instances are busy.", exception.Message);

        List<Task> waitConnectionTask = [];
        int connectionCompleted = 0;
        foreach (NamedPipeServer namedPipeServer in pipes)
        {
            waitConnectionTask.Add(Task.Run(
                async () =>
                {
                    await namedPipeServer.WaitConnectionAsync(CancellationToken.None);
                    Interlocked.Increment(ref connectionCompleted);
                }, CancellationToken.None));
        }

        List<NamedPipeClient> connectedClients = [];
        for (int i = 0; i < waitConnectionTask.Count; i++)
        {
            var namedPipeClient = new NamedPipeClient(pipeName);
            connectedClients.Add(namedPipeClient);
            await namedPipeClient.ConnectAsync(CancellationToken.None);
        }

        await Task.WhenAll([.. waitConnectionTask]);

        Assert.Equal(3, connectionCompleted);

        foreach (NamedPipeClient namedPipeClient in connectedClients)
        {
            namedPipeClient.Dispose();
        }

        foreach (NamedPipeServer namedPipeServer in pipes)
        {
            namedPipeServer.Dispose();
        }
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

        public object Deserialize(Stream stream) => new LongMessage(ReadInt(stream));

        public void Serialize(object objectToSerialize, Stream stream) => WriteLong(stream, ((LongMessage)objectToSerialize).Long);
    }

}
