// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

/*
|---FieldCount---| 2 bytes

|---ExecutionId Id---| (2 bytes)
|---ExecutionId Size---| (4 bytes)
|---ExecutionId Value---| (n bytes)

|---InstanceId---| (2 bytes)
|---InstanceId Size---| (4 bytes)
|---InstanceId Value---| (n bytes)

|---DiscoveredTestMessageList Id---| (2 bytes)
|---DiscoveredTestMessageList Size---| (4 bytes)
|---DiscoveredTestMessageList Value---| (n bytes)
    |---DiscoveredTestMessageList Length---| (4 bytes)

    |---DiscoveredTestMessageList[0] FieldCount---| 2 bytes

    |---DiscoveredTestMessageList[0].Uid Id---| (2 bytes)
    |---DiscoveredTestMessageList[0].Uid Size---| (4 bytes)
    |---DiscoveredTestMessageList[0].Uid Value---| (n bytes)

    |---DiscoveredTestMessageList[0].DisplayName Id---| (2 bytes)
    |---DiscoveredTestMessageList[0].DisplayName Size---| (4 bytes)
    |---DiscoveredTestMessageList[0].DisplayName Value---| (n bytes)

    |---DiscoveredTestMessageList[0].FilePath Id---| (2 bytes)
    |---DiscoveredTestMessageList[0].FilePath Size---| (4 bytes)
    |---DiscoveredTestMessageList[0].FilePath Value---| (n bytes)

    |---DiscoveredTestMessageList[0].LineNumber Id---| (2 bytes)
    |---DiscoveredTestMessageList[0].LineNumber Size---| (4 bytes)
    |---DiscoveredTestMessageList[0].LineNumber Value---| (4 bytes)

    |---DiscoveredTestMessageList[0].Namespace Id---| (2 bytes)
    |---DiscoveredTestMessageList[0].Namespace Size---| (4 bytes)
    |---DiscoveredTestMessageList[0].Namespace Value---| (n bytes)

    |---DiscoveredTestMessageList[0].TypeName Id---| (2 bytes)
    |---DiscoveredTestMessageList[0].TypeName Size---| (4 bytes)
    |---DiscoveredTestMessageList[0].TypeName Value---| (n bytes)

    |---DiscoveredTestMessageList[0].MethodName Id---| (2 bytes)
    |---DiscoveredTestMessageList[0].MethodName Size---| (4 bytes)
    |---DiscoveredTestMessageList[0].MethodName Value---| (n bytes)

    |---DiscoveredTestMessageList[0].ParameterTypeFullNames Id---| (2 bytes)
    |---DiscoveredTestMessageList[0].ParameterTypeFullNames Size---| (4 bytes)
    |---DiscoveredTestMessageList[0].ParameterTypeFullNames Value---| (n bytes)
        |---DiscoveredTestMessageList[0].ParameterTypeFullNames Length---| (4 bytes)

        |---DiscoveredTestMessageList[0].ParameterTypeFullNames[0] Size---| (4 bytes)
        |---DiscoveredTestMessageList[0].ParameterTypeFullNames[0] Value---| (n bytes)

    |---DiscoveredTestMessageList[0].Traits Id---| (2 bytes)
    |---DiscoveredTestMessageList[0].Traits Size---| (4 bytes)
    |---DiscoveredTestMessageList[0].Traits Value---| (n bytes)
        |---DiscoveredTestMessageList[0].Traits Length---| (4 bytes)

        |---DiscoveredTestMessageList[0].Traits[0] FieldCount---| 2 bytes

        |---DiscoveredTestMessageList[0].Traits[0].Key Id---| (2 bytes)
        |---DiscoveredTestMessageList[0].Traits[0].Key Size---| (4 bytes)
        |---DiscoveredTestMessageList[0].Traits[0].Key Value---| (n bytes)

        |---DiscoveredTestMessageList[0].Traits[0].Value Id---| (2 bytes)
        |---DiscoveredTestMessageList[0].Traits[0].Value Size---| (4 bytes)
        |---DiscoveredTestMessageList[0].Traits[0].Value Value---| (n bytes)
*/

internal sealed class DiscoveredTestMessagesSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => DiscoveredTestMessagesFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        List<DiscoveredTestMessage>? discoveredTestMessages = null;

        ushort fieldCount = ReadUShort(stream);

        for (int i = 0; i < fieldCount; i++)
        {
            int fieldId = ReadUShort(stream);
            int fieldSize = ReadInt(stream);

            switch (fieldId)
            {
                case DiscoveredTestMessagesFieldsId.ExecutionId:
                    executionId = ReadStringValue(stream, fieldSize);
                    break;

                case DiscoveredTestMessagesFieldsId.InstanceId:
                    instanceId = ReadStringValue(stream, fieldSize);
                    break;

                case DiscoveredTestMessagesFieldsId.DiscoveredTestMessageList:
                    discoveredTestMessages = ReadDiscoveredTestMessagesPayload(stream);
                    break;

                default:
                    // If we don't recognize the field id, skip the payload corresponding to that field
                    SetPosition(stream, stream.Position + fieldSize);
                    break;
            }
        }

        return new DiscoveredTestMessages(executionId, instanceId, discoveredTestMessages is null ? [] : [.. discoveredTestMessages]);
    }

    private static List<DiscoveredTestMessage> ReadDiscoveredTestMessagesPayload(Stream stream)
    {
        List<DiscoveredTestMessage> discoveredTestMessages = [];

        int length = ReadInt(stream);
        for (int i = 0; i < length; i++)
        {
            string? uid = null;
            string? displayName = null;
            string? filePath = null;
            int? lineNumber = null;
            string? @namespace = null;
            string? typeName = null;
            string? methodName = null;
            string[] parameterTypeFullNames = [];
            TraitMessage[] traits = [];

            int fieldCount = ReadUShort(stream);

            for (int j = 0; j < fieldCount; j++)
            {
                int fieldId = ReadUShort(stream);
                int fieldSize = ReadInt(stream);

                switch (fieldId)
                {
                    case DiscoveredTestMessageFieldsId.Uid:
                        uid = ReadStringValue(stream, fieldSize);
                        break;

                    case DiscoveredTestMessageFieldsId.DisplayName:
                        displayName = ReadStringValue(stream, fieldSize);
                        break;

                    case DiscoveredTestMessageFieldsId.FilePath:
                        filePath = ReadStringValue(stream, fieldSize);
                        break;

                    case DiscoveredTestMessageFieldsId.LineNumber:
                        lineNumber = ReadInt(stream);
                        break;

                    case DiscoveredTestMessageFieldsId.Namespace:
                        @namespace = ReadStringValue(stream, fieldSize);
                        break;

                    case DiscoveredTestMessageFieldsId.TypeName:
                        typeName = ReadStringValue(stream, fieldSize);
                        break;

                    case DiscoveredTestMessageFieldsId.MethodName:
                        methodName = ReadStringValue(stream, fieldSize);
                        break;

                    case DiscoveredTestMessageFieldsId.ParameterTypeFullNames:
                        parameterTypeFullNames = ReadParameterTypeFullNamesPayload(stream);
                        break;

                    case DiscoveredTestMessageFieldsId.Traits:
                        traits = ReadTraitsPayload(stream);
                        break;

                    default:
                        SetPosition(stream, stream.Position + fieldSize);
                        break;
                }
            }

            discoveredTestMessages.Add(new DiscoveredTestMessage(uid, displayName, filePath, lineNumber, @namespace, typeName, methodName, parameterTypeFullNames, traits));
        }

        return discoveredTestMessages;
    }

    private static string[] ReadParameterTypeFullNamesPayload(Stream stream)
    {
        int length = ReadInt(stream);
        string[] parameterTypeFullNames = new string[length];

        for (int i = 0; i < length; i++)
        {
            parameterTypeFullNames[i] = ReadString(stream);
        }

        return parameterTypeFullNames;
    }

    private static TraitMessage[] ReadTraitsPayload(Stream stream)
    {
        int length = ReadInt(stream);
        var traits = new TraitMessage[length];
        for (int i = 0; i < length; i++)
        {
            string? key = null;
            string? value = null;
            int fieldCount = ReadUShort(stream);

            for (int j = 0; j < fieldCount; j++)
            {
                int fieldId = ReadUShort(stream);
                int fieldSize = ReadInt(stream);

                switch (fieldId)
                {
                    case TraitMessageFieldsId.Key:
                        key = ReadStringValue(stream, fieldSize);
                        break;

                    case TraitMessageFieldsId.Value:
                        value = ReadStringValue(stream, fieldSize);
                        break;

                    default:
                        SetPosition(stream, stream.Position + fieldSize);
                        break;
                }
            }

            if (key is null || value is null)
            {
                throw new InvalidOperationException("Trait message is missing Key or Value field.");
            }

            traits[i] = new TraitMessage(key, value);
        }

        return traits;
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

        var discoveredTestMessages = (DiscoveredTestMessages)objectToSerialize;

        WriteUShort(stream, GetFieldCount(discoveredTestMessages));

        WriteField(stream, DiscoveredTestMessagesFieldsId.ExecutionId, discoveredTestMessages.ExecutionId);
        WriteField(stream, DiscoveredTestMessagesFieldsId.InstanceId, discoveredTestMessages.InstanceId);
        WriteDiscoveredTestMessagesPayload(stream, discoveredTestMessages.DiscoveredMessages);
    }

    private static void WriteDiscoveredTestMessagesPayload(Stream stream, DiscoveredTestMessage[]? discoveredTestMessageList)
    {
        if (discoveredTestMessageList is null || discoveredTestMessageList.Length == 0)
        {
            return;
        }

        WriteUShort(stream, DiscoveredTestMessagesFieldsId.DiscoveredTestMessageList);

        // We will reserve an int (4 bytes)
        // so that we fill the size later, once we write the payload
        WriteInt(stream, 0);

        long before = stream.Position;
        WriteInt(stream, discoveredTestMessageList.Length);
        foreach (DiscoveredTestMessage discoveredTestMessage in discoveredTestMessageList)
        {
            WriteUShort(stream, GetFieldCount(discoveredTestMessage));

            WriteField(stream, DiscoveredTestMessageFieldsId.Uid, discoveredTestMessage.Uid);
            WriteField(stream, DiscoveredTestMessageFieldsId.DisplayName, discoveredTestMessage.DisplayName);
            WriteField(stream, DiscoveredTestMessageFieldsId.FilePath, discoveredTestMessage.FilePath);
            WriteField(stream, DiscoveredTestMessageFieldsId.LineNumber, discoveredTestMessage.LineNumber);
            WriteField(stream, DiscoveredTestMessageFieldsId.Namespace, discoveredTestMessage.Namespace);
            WriteField(stream, DiscoveredTestMessageFieldsId.TypeName, discoveredTestMessage.TypeName);
            WriteField(stream, DiscoveredTestMessageFieldsId.MethodName, discoveredTestMessage.MethodName);
            WriteParameterTypeFullNamesPayload(stream, discoveredTestMessage.ParameterTypeFullNames);
            WriteTraitsPayload(stream, discoveredTestMessage.Traits);
        }

        // NOTE: We are able to seek only if we are using a MemoryStream
        // thus, the seek operation is fast as we are only changing the value of a property
        WriteAtPosition(stream, (int)(stream.Position - before), before - sizeof(int));
    }

    private static void WriteParameterTypeFullNamesPayload(Stream stream, string[]? parameterTypeFullNames)
    {
        if (parameterTypeFullNames is null || parameterTypeFullNames.Length == 0)
        {
            return;
        }

        WriteUShort(stream, DiscoveredTestMessageFieldsId.ParameterTypeFullNames);

        // We will reserve an int (4 bytes)
        // so that we fill the size later, once we write the payload
        WriteInt(stream, 0);

        long before = stream.Position;
        WriteInt(stream, parameterTypeFullNames.Length);
        foreach (string parameterTypeFullName in parameterTypeFullNames)
        {
            WriteString(stream, parameterTypeFullName);
        }

        // NOTE: We are able to seek only if we are using a MemoryStream
        // thus, the seek operation is fast as we are only changing the value of a property
        WriteAtPosition(stream, (int)(stream.Position - before), before - sizeof(int));
    }

    private static void WriteTraitsPayload(Stream stream, TraitMessage[]? traits)
    {
        if (traits is null || traits.Length == 0)
        {
            return;
        }

        WriteUShort(stream, DiscoveredTestMessageFieldsId.Traits);

        // We will reserve an int (4 bytes)
        // so that we fill the size later, once we write the payload
        WriteInt(stream, 0);

        long before = stream.Position;
        WriteInt(stream, traits.Length);
        foreach (TraitMessage trait in traits)
        {
            WriteUShort(stream, GetFieldCount(trait));

            WriteField(stream, TraitMessageFieldsId.Key, trait.Key);
            WriteField(stream, TraitMessageFieldsId.Value, trait.Value);
        }

        // NOTE: We are able to seek only if we are using a MemoryStream
        // thus, the seek operation is fast as we are only changing the value of a property
        WriteAtPosition(stream, (int)(stream.Position - before), before - sizeof(int));
    }

    private static ushort GetFieldCount(DiscoveredTestMessages discoveredTestMessages) =>
        (ushort)((discoveredTestMessages.ExecutionId is null ? 0 : 1) +
        (discoveredTestMessages.InstanceId is null ? 0 : 1) +
        (IsNullOrEmpty(discoveredTestMessages.DiscoveredMessages) ? 0 : 1));

    private static ushort GetFieldCount(DiscoveredTestMessage discoveredTestMessage) =>
        (ushort)((discoveredTestMessage.Uid is null ? 0 : 1) +
        (discoveredTestMessage.DisplayName is null ? 0 : 1) +
        (discoveredTestMessage.FilePath is null ? 0 : 1) +
        (discoveredTestMessage.LineNumber is null ? 0 : 1) +
        (discoveredTestMessage.Namespace is null ? 0 : 1) +
        (discoveredTestMessage.TypeName is null ? 0 : 1) +
        (discoveredTestMessage.MethodName is null ? 0 : 1) +
        (IsNullOrEmpty(discoveredTestMessage.ParameterTypeFullNames) ? 0 : 1) +
        (IsNullOrEmpty(discoveredTestMessage.Traits) ? 0 : 1));

    private static ushort GetFieldCount(TraitMessage trait) =>
        (ushort)((trait.Key is null ? 0 : 1) +
        (trait.Value is null ? 0 : 1));
}
