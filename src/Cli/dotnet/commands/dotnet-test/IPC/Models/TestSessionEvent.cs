// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace Microsoft.DotNet.Tools.Test
{
    internal sealed record TestSessionEvent(byte? SessionType, string? SessionUid, string? ExecutionId) : IRequest;
}
