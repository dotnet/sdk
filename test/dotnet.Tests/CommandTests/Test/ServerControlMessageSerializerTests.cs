// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class ServerControlMessageSerializerTests
{
    [TestMethod]
    public void ServerControlMessageRoundTrips()
    {
        var serializer = new ServerControlMessageSerializer();
        var message = new ServerControlMessage(ServerControlKinds.CancelSession);
        using var stream = new MemoryStream();

        serializer.Serialize(message, stream);
        stream.Position = 0;

        serializer.Deserialize(stream).Should().Be(message);
    }

    [TestMethod]
    public void SerializerIdsMatchTestFxContract()
    {
        new WaitForServerControlRequestSerializer().Id.Should().Be(13);
        new ServerControlMessageSerializer().Id.Should().Be(14);
    }

    [TestMethod]
    public void WaitForServerControlRequestHasNoPayload()
    {
        var serializer = new WaitForServerControlRequestSerializer();
        using var stream = new MemoryStream();

        serializer.Serialize(WaitForServerControlRequest.CachedInstance, stream);

        stream.Length.Should().Be(0);
        serializer.Deserialize(stream).Should().BeSameAs(WaitForServerControlRequest.CachedInstance);
    }
}
