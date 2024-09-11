﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Test
{
    internal sealed class ModuleMessageSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => ModuleFieldsId.MessagesSerializerId;

        public object Deserialize(Stream stream)
        {
            string modulePath = ReadString(stream);
            string projectPath = ReadString(stream);
            string targetFramework = ReadString(stream);
            return new ModuleMessage(modulePath.Trim(), projectPath.Trim(), targetFramework.Trim());
        }

        public void Serialize(object objectToSerialize, Stream stream)
        {
            WriteString(stream, ((ModuleMessage)objectToSerialize).DLLPath);
            WriteString(stream, ((ModuleMessage)objectToSerialize).ProjectPath);
            WriteString(stream, ((ModuleMessage)objectToSerialize).TargetFramework);
        }
    }
}
