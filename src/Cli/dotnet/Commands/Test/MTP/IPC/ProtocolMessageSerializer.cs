// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC;

internal sealed class ProtocolMessageSerializer : NamedPipeBase
{
    private const int FrameHeaderSize = sizeof(int) + sizeof(int);

    public object Deserialize(byte[] frame, bool skipUnknownMessages)
    {
        if (frame.Length < FrameHeaderSize)
        {
            throw new InvalidDataException("The dotnet test protocol frame is shorter than its header.");
        }

        int payloadLength = BitConverter.ToInt32(frame.AsSpan());
        if (payloadLength < sizeof(int) || payloadLength != frame.Length - sizeof(int))
        {
            throw new InvalidDataException("The dotnet test protocol frame length is invalid.");
        }

        int serializerId = BitConverter.ToInt32(frame.AsSpan(sizeof(int)));
        INamedPipeSerializer serializer = GetSerializer(serializerId, skipUnknownMessages);

        using var body = new MemoryStream(
            frame,
            FrameHeaderSize,
            frame.Length - FrameHeaderSize,
            writable: false);
        return serializer.Deserialize(body);
    }

    public byte[] Serialize(object message)
    {
        INamedPipeSerializer serializer = GetSerializer(message.GetType());

        using var body = new MemoryStream();
        serializer.Serialize(message, body);

        int payloadLength = checked(sizeof(int) + (int)body.Length);
        byte[] frame = new byte[checked(sizeof(int) + payloadLength)];
        if (!BitConverter.TryWriteBytes(frame.AsSpan(0, sizeof(int)), payloadLength) ||
            !BitConverter.TryWriteBytes(frame.AsSpan(sizeof(int), sizeof(int)), serializer.Id))
        {
            throw new UnreachableException();
        }

        body.GetBuffer().AsSpan(0, (int)body.Length).CopyTo(frame.AsSpan(FrameHeaderSize));
        return frame;
    }
}
