// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Test
{
    internal sealed class ModuleSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => 4;

        public object Deserialize(Stream stream)
        {
            string modulePath = ReadString(stream);
            string projectPath = ReadString(stream);
            return new Module(modulePath.Trim(), projectPath.Trim());
        }

        public void Serialize(object objectToSerialize, Stream stream)
        {
            WriteString(stream, ((Module)objectToSerialize).DLLPath);
            WriteString(stream, ((Module)objectToSerialize).ProjectPath);
        }
    }
}
