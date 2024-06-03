// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Test
{
    internal sealed class ModuleSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => 4;

        public object Deserialize(Stream stream)
        {
            string moduleName = ReadString(stream);
            return new Module(moduleName.Trim());
        }

        public void Serialize(object objectToSerialize, Stream stream)
        {
            WriteString(stream, ((Module)objectToSerialize).Name);
        }
    }
}
