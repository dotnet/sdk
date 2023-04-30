// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.NET.Build.Containers;

public static class KnownDaemonTypes
{
    public const string Docker = nameof(Docker);
    public static readonly string[] SupportedLocalDaemonTypes = new [] { Docker };
}
