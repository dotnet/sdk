// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

internal sealed class InvocationSetupCompletedSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => InvocationSetupCompletedFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        bool succeeded = false;
        string? errorMessage = null;
        InvocationLifetimeOutput[] outputs = [];

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            if (TryReadExecutionScopedField(stream, fieldId, fieldSize, ref executionId, ref instanceId))
            {
                return true;
            }

            switch (fieldId)
            {
                case InvocationSetupCompletedFieldsId.Succeeded:
                    succeeded = ReadFieldPayload(stream, fieldSize, ReadBool);
                    return true;
                case InvocationSetupCompletedFieldsId.ErrorMessage:
                    errorMessage = ReadStringValue(stream, fieldSize);
                    return true;
                case InvocationSetupCompletedFieldsId.Outputs:
                    outputs = ReadFieldPayload(stream, fieldSize, ReadOutputs);
                    return true;
                default:
                    return false;
            }
        });

        return new InvocationSetupCompleted(executionId, instanceId, succeeded, errorMessage, outputs);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var message = (InvocationSetupCompleted)objectToSerialize;
        WriteExecutionScopedHeader(stream, message.ExecutionId, message.InstanceId, (ushort)(1
            + (message.ErrorMessage is null ? 0 : 1)
            + (IsNullOrEmpty(message.Outputs) ? 0 : 1)));
        WriteField(stream, InvocationSetupCompletedFieldsId.Succeeded, message.Succeeded);
        WriteField(stream, InvocationSetupCompletedFieldsId.ErrorMessage, message.ErrorMessage);
        WriteListPayload(stream, InvocationSetupCompletedFieldsId.Outputs, message.Outputs, static (s, output) =>
        {
            bool writeValue = output.HasValue && output.Value is not null;
            WriteUShort(s, (ushort)(2 + (writeValue ? 1 : 0)));
            WriteField(s, InvocationLifetimeOutputFieldsId.Name, output.Name);
            WriteField(s, InvocationLifetimeOutputFieldsId.HasValue, output.HasValue);
            if (writeValue)
            {
                WriteField(s, InvocationLifetimeOutputFieldsId.Value, output.Value);
            }
        });
    }

    private static InvocationLifetimeOutput[] ReadOutputs(Stream stream)
    {
        int length = ReadCollectionLength(stream, sizeof(ushort));
        var outputs = new InvocationLifetimeOutput[length];
        for (int i = 0; i < length; i++)
        {
            string? name = null;
            bool hasValue = false;
            string? value = null;
            ReadFields(stream, (fieldId, fieldSize) =>
            {
                switch (fieldId)
                {
                    case InvocationLifetimeOutputFieldsId.Name:
                        name = ReadStringValue(stream, fieldSize);
                        return true;
                    case InvocationLifetimeOutputFieldsId.HasValue:
                        hasValue = ReadFieldPayload(stream, fieldSize, ReadBool);
                        return true;
                    case InvocationLifetimeOutputFieldsId.Value:
                        value = ReadStringValue(stream, fieldSize);
                        return true;
                    default:
                        return false;
                }
            });

            if (name is null)
            {
                throw new InvalidOperationException("Invocation lifetime output is missing its name.");
            }

            outputs[i] = new InvocationLifetimeOutput(name, hasValue, hasValue ? value ?? string.Empty : null);
        }

        return outputs;
    }
}

internal sealed class ModuleTestsCompletedSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => ModuleTestsCompletedFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        int exitCode = 0;

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            if (TryReadExecutionScopedField(stream, fieldId, fieldSize, ref executionId, ref instanceId))
            {
                return true;
            }

            if (fieldId == ModuleTestsCompletedFieldsId.ExitCode)
            {
                exitCode = ReadFieldPayload(stream, fieldSize, ReadInt);
                return true;
            }

            return false;
        });

        return new ModuleTestsCompleted(executionId, instanceId, exitCode);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var message = (ModuleTestsCompleted)objectToSerialize;
        WriteExecutionScopedHeader(stream, message.ExecutionId, message.InstanceId, 1);
        WriteField(stream, ModuleTestsCompletedFieldsId.ExitCode, message.ExitCode);
    }
}

internal sealed class InvocationCleanupCompletedSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => InvocationCleanupCompletedFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        bool succeeded = false;
        string? errorMessage = null;

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            if (TryReadExecutionScopedField(stream, fieldId, fieldSize, ref executionId, ref instanceId))
            {
                return true;
            }

            switch (fieldId)
            {
                case InvocationCleanupCompletedFieldsId.Succeeded:
                    succeeded = ReadFieldPayload(stream, fieldSize, ReadBool);
                    return true;
                case InvocationCleanupCompletedFieldsId.ErrorMessage:
                    errorMessage = ReadStringValue(stream, fieldSize);
                    return true;
                default:
                    return false;
            }
        });

        return new InvocationCleanupCompleted(executionId, instanceId, succeeded, errorMessage);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var message = (InvocationCleanupCompleted)objectToSerialize;
        WriteExecutionScopedHeader(stream, message.ExecutionId, message.InstanceId, (ushort)(1 + (message.ErrorMessage is null ? 0 : 1)));
        WriteField(stream, InvocationCleanupCompletedFieldsId.Succeeded, message.Succeeded);
        WriteField(stream, InvocationCleanupCompletedFieldsId.ErrorMessage, message.ErrorMessage);
    }
}