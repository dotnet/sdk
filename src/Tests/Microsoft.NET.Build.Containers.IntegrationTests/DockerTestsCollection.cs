// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

[CollectionDefinition("Docker tests")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public class DockerTestsCollection : ICollectionFixture<DockerTestsFixture>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
