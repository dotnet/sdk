// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.NET.Build.Containers;

// Explicitly lowercase to ease parsing - the incoming values are
// lowercased by spec
public enum PortType
{
    tcp,
    udp
}

public record struct Port(int Number, PortType Type);
