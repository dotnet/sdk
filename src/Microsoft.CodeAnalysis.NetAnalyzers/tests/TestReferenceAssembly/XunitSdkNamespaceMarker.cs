// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xunit.Sdk
{
    /// <summary>
    /// Provides a public type in the <c>Xunit.Sdk</c> namespace so that analyzer tests which
    /// verify type-name/namespace collisions (for example CA1724 TypeNamesShouldNotMatchNamespaces)
    /// can reference an assembly contributing that namespace without taking a dependency on the
    /// real xUnit packages. This is test fixture data only and is unrelated to the test framework.
    /// </summary>
    public sealed class NamespaceCollisionMarker
    {
    }
}
