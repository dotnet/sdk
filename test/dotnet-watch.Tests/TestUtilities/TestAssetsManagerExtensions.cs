// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Watch.UnitTests;

internal static class TestAssetsManagerExtensions
{
    extension(TestAssetsManager)
    {
        public static TestDirectory CreateTestDirectory([CallerMemberName] string? testName = null, object[]? identifiers = null)
            => TestDirectory.Create(TestAssetsManager.GetTestDestinationDirectoryPath(
                testName,
                testName,
                identifiers != null ? string.Join(';', identifiers.Select(id => id != null ? "_" + id.ToString() : "null")) : string.Empty,
                baseDirectory: null));
    }
}
