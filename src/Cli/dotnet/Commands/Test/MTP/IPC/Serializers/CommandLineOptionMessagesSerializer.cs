// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

/*
|---FieldCount---| 2 bytes

|---ModuleName Id---| (2 bytes)
|---ModuleName Size---| (4 bytes)
|---ModuleName Value---| (n bytes)

|---CommandLineOptionMessageList Id---| (2 bytes)
|---CommandLineOptionMessageList Size---| (4 bytes)
|---CommandLineOptionMessageList Value---| (n bytes)
    |---CommandLineOptionMessageList Length---| (4 bytes)

    |---CommandLineOptionMessageList[0] FieldCount---| 2 bytes

    |---CommandLineOptionMessageList[0].Name Id---| (2 bytes)
    |---CommandLineOptionMessageList[0].Name Size---| (4 bytes)
    |---CommandLineOptionMessageList[0].Name Value---| (n bytes)

    |---CommandLineOptionMessageList[0].Description Id---| (2 bytes)
    |---CommandLineOptionMessageList[0].Description Size---| (4 bytes)
    |---CommandLineOptionMessageList[0].Description Value---| (n bytes)

    |---CommandLineOptionMessageList[0].IsHidden Id---| (2 bytes)
    |---CommandLineOptionMessageList[0].IsHidden Size---| (4 bytes)
    |---CommandLineOptionMessageList[0].IsHidden Value---| (1 byte)

    |---CommandLineOptionMessageList[0].IsBuiltIn Id---| (2 bytes)
    |---CommandLineOptionMessageList[0].IsBuiltIn Size---| (4 bytes)
    |---CommandLineOptionMessageList[0].IsBuiltIn Value---| (1 byte)
*/

internal sealed class CommandLineOptionMessagesSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => CommandLineOptionMessagesFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        string? moduleName = null;
        List<CommandLineOptionMessage>? commandLineOptionMessages = null;

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            switch (fieldId)
            {
                case CommandLineOptionMessagesFieldsId.ModulePath:
                    moduleName = ReadStringValue(stream, fieldSize);
                    return true;

                case CommandLineOptionMessagesFieldsId.CommandLineOptionMessageList:
                    commandLineOptionMessages = ReadCommandLineOptionMessagesPayload(stream);
                    return true;

                default:
                    return false;
            }
        });

        return new CommandLineOptionMessages(moduleName, commandLineOptionMessages is null ? [] : [.. commandLineOptionMessages]);
    }

    private static List<CommandLineOptionMessage> ReadCommandLineOptionMessagesPayload(Stream stream)
    {
        List<CommandLineOptionMessage> commandLineOptionMessages = [];

        int length = ReadInt(stream);
        for (int i = 0; i < length; i++)
        {
            string? name = null, description = null;
            bool? isHidden = null, isBuiltIn = null;

            ReadFields(stream, (fieldId, fieldSize) =>
            {
                switch (fieldId)
                {
                    case CommandLineOptionMessageFieldsId.Name:
                        name = ReadStringValue(stream, fieldSize);
                        return true;

                    case CommandLineOptionMessageFieldsId.Description:
                        description = ReadStringValue(stream, fieldSize);
                        return true;

                    case CommandLineOptionMessageFieldsId.IsHidden:
                        isHidden = ReadBool(stream);
                        return true;

                    case CommandLineOptionMessageFieldsId.IsBuiltIn:
                        isBuiltIn = ReadBool(stream);
                        return true;

                    default:
                        return false;
                }
            });

            commandLineOptionMessages.Add(new CommandLineOptionMessage(name, description, isHidden, isBuiltIn));
        }

        return commandLineOptionMessages;
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

        var commandLineOptionMessages = (CommandLineOptionMessages)objectToSerialize;

        WriteUShort(stream, GetFieldCount(commandLineOptionMessages));

        WriteField(stream, CommandLineOptionMessagesFieldsId.ModulePath, commandLineOptionMessages.ModulePath);
        WriteCommandLineOptionMessagesPayload(stream, commandLineOptionMessages.CommandLineOptionMessageList);
    }

    private static void WriteCommandLineOptionMessagesPayload(Stream stream, CommandLineOptionMessage[]? commandLineOptionMessageList)
        => WriteListPayload(stream, CommandLineOptionMessagesFieldsId.CommandLineOptionMessageList, commandLineOptionMessageList, static (s, commandLineOptionMessage) =>
        {
            WriteUShort(s, GetFieldCount(commandLineOptionMessage));

            WriteField(s, CommandLineOptionMessageFieldsId.Name, commandLineOptionMessage.Name);
            WriteField(s, CommandLineOptionMessageFieldsId.Description, commandLineOptionMessage.Description);
            WriteField(s, CommandLineOptionMessageFieldsId.IsHidden, commandLineOptionMessage.IsHidden);
            WriteField(s, CommandLineOptionMessageFieldsId.IsBuiltIn, commandLineOptionMessage.IsBuiltIn);
        });

    private static ushort GetFieldCount(CommandLineOptionMessages commandLineOptionMessages) =>
        (ushort)((commandLineOptionMessages.ModulePath is null ? 0 : 1) +
        (IsNullOrEmpty(commandLineOptionMessages.CommandLineOptionMessageList) ? 0 : 1));

    private static ushort GetFieldCount(CommandLineOptionMessage commandLineOptionMessage) =>
        (ushort)((commandLineOptionMessage.Name is null ? 0 : 1) +
        (commandLineOptionMessage.Description is null ? 0 : 1) +
        (commandLineOptionMessage.IsHidden is null ? 0 : 1) +
        (commandLineOptionMessage.IsBuiltIn is null ? 0 : 1));
}
