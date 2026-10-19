// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.DotNet.Cli.Commands.Test.IPC;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public sealed class CommandLineOptionMessagesSerializerTests
{
    [TestMethod]
    public void FieldIds_MatchUpstreamContract()
    {
        Assert.AreEqual(3, new CommandLineOptionMessagesSerializer().Id);
        Assert.AreSequenceEqual(
            new ushort[] { 6, 7, 8 },
            new[]
            {
                CommandLineOptionMessageFieldsId.ProviderUid,
                CommandLineOptionMessageFieldsId.MinimumArity,
                CommandLineOptionMessageFieldsId.MaximumArity,
            });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Deserialize_PreservesKnownFields_WithOrWithoutOptionalMetadata(bool includeMetadata)
    {
        using var listPayload = new MemoryStream();
        using (var writer = new BinaryWriter(listPayload, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(2);
            foreach (string name in new[] { "first-option", "second-option" })
            {
                writer.Write((ushort)(includeMetadata ? 7 : 4));
                if (includeMetadata)
                {
                    WriteStringField(writer, CommandLineOptionMessageFieldsId.ProviderUid, "provider-uid");
                }

                WriteStringField(writer, CommandLineOptionMessageFieldsId.Name, name);
                if (includeMetadata)
                {
                    writer.Write(CommandLineOptionMessageFieldsId.MinimumArity);
                    writer.Write(sizeof(int));
                    writer.Write(0);
                }

                writer.Write(CommandLineOptionMessageFieldsId.IsHidden);
                writer.Write(sizeof(bool));
                writer.Write(false);
                if (includeMetadata)
                {
                    writer.Write(CommandLineOptionMessageFieldsId.MaximumArity);
                    writer.Write(sizeof(int));
                    writer.Write(int.MaxValue);
                }

                WriteStringField(writer, CommandLineOptionMessageFieldsId.Description, "Option description");
                writer.Write(CommandLineOptionMessageFieldsId.IsBuiltIn);
                writer.Write(sizeof(bool));
                writer.Write(true);
            }
        }

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)2);
            writer.Write(CommandLineOptionMessagesFieldsId.CommandLineOptionMessageList);
            writer.Write(checked((int)listPayload.Length));
            writer.Write(listPayload.ToArray());
            WriteStringField(writer, CommandLineOptionMessagesFieldsId.ModulePath, "test-module.dll");
        }

        stream.Position = 0;
        var message = (CommandLineOptionMessages)new CommandLineOptionMessagesSerializer().Deserialize(stream);

        Assert.AreEqual("test-module.dll", message.ModulePath);
        Assert.AreSequenceEqual(
            new[]
            {
                new CommandLineOptionMessage("first-option", "Option description", false, true),
                new CommandLineOptionMessage("second-option", "Option description", false, true),
            },
            message.CommandLineOptionMessageList);
        Assert.AreEqual(stream.Length, stream.Position);
    }

    private static void WriteStringField(BinaryWriter writer, ushort fieldId, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(fieldId);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}
