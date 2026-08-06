// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.IPC;

internal interface INamedPipeSerializer
{
    int Id { get; }

    void Serialize(object objectToSerialize, Stream stream);

    object Deserialize(Stream stream);
}
