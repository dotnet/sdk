// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Diagnostics;
using Microsoft.DotNet.Cli.commands.dotnet_test.IPC;

namespace Microsoft.DotNet.Tools.Test
{
    internal sealed class CommandLineOptionMessagesSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => 3;

        public object Deserialize(Stream stream)
        {
            string moduleName = string.Empty;
            List<CommandLineOptionMessage>? commandLineOptionMessages = null;

            short fieldCount = ReadShort(stream);

            for (int i = 0; i < fieldCount; i++)
            {
                int fieldId = ReadShort(stream);
                var fieldSize = ReadInt(stream);

                switch (fieldId)
                {
                    case CommandLineOptionMessagesFields.ModuleName:
                        moduleName = ReadString(stream);
                        break;

                    case CommandLineOptionMessagesFields.CommandLineOptionMessageList:
                        commandLineOptionMessages = ReadCommandLineOptionMessagesPayload(stream);
                        break;

                    default:
                        // If we don't recognize the field id, skip the payload corresponding to that field
                        SetPosition(stream, stream.Position + fieldSize);
                        break;
                }
            }

            return new CommandLineOptionMessages(moduleName, commandLineOptionMessages is null ? [] : [.. commandLineOptionMessages]);
        }

        private static List<CommandLineOptionMessage> ReadCommandLineOptionMessagesPayload(Stream stream)
        {
            List<CommandLineOptionMessage> commandLineOptionMessages = [];

            int length = ReadInt(stream);
            for (int i = 0; i < length; i++)
            {
                string name = string.Empty, description = string.Empty, arity = string.Empty;
                bool isHidden = false, isBuiltIn = false;

                int fieldCount = ReadShort(stream);

                for (int j = 0; j < fieldCount; j++)
                {
                    int fieldId = ReadShort(stream);
                    var fieldSize = ReadInt(stream);

                    switch (fieldId)
                    {
                        case CommandLineOptionMessageFields.Name:
                            name = ReadString(stream);
                            break;

                        case CommandLineOptionMessageFields.Description:
                            description = ReadString(stream);
                            break;

                        case CommandLineOptionMessageFields.Arity:
                            arity = ReadString(stream);
                            break;

                        case CommandLineOptionMessageFields.IsHidden:
                            isHidden = ReadBool(stream);
                            break;

                        case CommandLineOptionMessageFields.IsBuiltIn:
                            isBuiltIn = ReadBool(stream);
                            break;

                        default:
                            SetPosition(stream, stream.Position + fieldSize);
                            break;
                    }
                }

                commandLineOptionMessages.Add(new CommandLineOptionMessage(name, description, arity, isHidden, isBuiltIn));
            }

            return commandLineOptionMessages;
        }

        public void Serialize(object objectToSerialize, Stream stream)
        {
            Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

            CommandLineOptionMessages commandLineOptionMessages = (CommandLineOptionMessages)objectToSerialize;

            WriteShort(stream, (short)GetFieldCount(commandLineOptionMessages));

            WriteField(stream, CommandLineOptionMessagesFields.ModuleName, commandLineOptionMessages.ModuleName);
            WriteCommandLineOptionMessagesPayload(stream, CommandLineOptionMessagesFields.CommandLineOptionMessageList, commandLineOptionMessages.CommandLineOptionMessageList);
        }

        private static void WriteCommandLineOptionMessagesPayload(Stream stream, short id, CommandLineOptionMessage[] commandLineOptionMessageList)
        {
            if (commandLineOptionMessageList is null)
            {
                return;
            }

            WriteShort(stream, id);

            // We will reserve an int (4 bytes)
            // so that we fill the size later, once we write the payload
            WriteInt(stream, 0);

            long before = stream.Position;
            WriteInt(stream, commandLineOptionMessageList.Length);
            foreach (CommandLineOptionMessage commandLineOptionMessage in commandLineOptionMessageList)
            {
                WriteShort(stream, (short)GetFieldCount(commandLineOptionMessage));

                WriteField(stream, CommandLineOptionMessageFields.Name, commandLineOptionMessage.Name);
                WriteField(stream, CommandLineOptionMessageFields.Description, commandLineOptionMessage.Description);
                WriteField(stream, CommandLineOptionMessageFields.Arity, commandLineOptionMessage.Arity);
                WriteField(stream, CommandLineOptionMessageFields.IsHidden, commandLineOptionMessage.IsHidden);
                WriteField(stream, CommandLineOptionMessageFields.IsBuiltIn, commandLineOptionMessage.IsBuiltIn);
            }

            // NOTE: We are able to seek only if we are using a MemoryStream
            // thus, the seek operation is fast as we are only changing the value of a property
            WriteAtPosition(stream, (int)(stream.Position - before), before - sizeof(int));
        }

        private static int GetFieldCount(CommandLineOptionMessages commandLineOptionMessages)
        {
            return (IsNull(commandLineOptionMessages.ModuleName) ? 1 : 0) +
               (commandLineOptionMessages.CommandLineOptionMessageList is null ? 0 : 1);
        }

        private static int GetFieldCount(CommandLineOptionMessage commandLineOptionMessage)
        {
            return (IsNull(commandLineOptionMessage.Name) ? 1 : 0) +
                (IsNull(commandLineOptionMessage.Description) ? 1 : 0) +
                (IsNull(commandLineOptionMessage.Arity) ? 1 : 0) +
                (IsNull(commandLineOptionMessage.IsHidden) ? 1 : 0) +
                (IsNull(commandLineOptionMessage.IsBuiltIn) ? 1 : 0);
        }

        private static bool IsNull<T>(T field)
        {
            return typeof(T) == typeof(string) ? field is not null : true;
        }
    }
}
