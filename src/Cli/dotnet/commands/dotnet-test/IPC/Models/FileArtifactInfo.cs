// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace Microsoft.DotNet.Tools.Test
{
    internal sealed record FileArtifactInfo(string? FullPath, string? DisplayName, string? Description, string? TestUid, string? TestDisplayName, string? SessionUid, string? ModulePath) : IRequest;
}
