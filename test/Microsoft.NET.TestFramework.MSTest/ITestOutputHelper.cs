// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework;

/// <summary>
/// Runner-agnostic test output abstraction used throughout the shared test framework sources.
///
/// The shared (link-shared) framework files reference the unqualified name
/// <c>ITestOutputHelper</c>. In the legacy xUnit framework project that name binds to
/// <c>Xunit.ITestOutputHelper</c> (via a global <c>using Xunit</c>); in this MSTest-flavored
/// project it binds to this interface instead. The shape is intentionally identical to
/// xUnit v3's <c>ITestOutputHelper</c> so the shared sources compile unchanged.
/// </summary>
public interface ITestOutputHelper
{
    /// <summary>
    /// Gets everything written to this output helper so far.
    /// </summary>
    string Output { get; }

    /// <summary>
    /// Writes a message to the test output.
    /// </summary>
    void Write(string message);

    /// <summary>
    /// Writes a formatted message to the test output.
    /// </summary>
    void Write(string format, params object[] args);

    /// <summary>
    /// Writes a message followed by a line break to the test output.
    /// </summary>
    void WriteLine(string message);

    /// <summary>
    /// Writes a formatted message followed by a line break to the test output.
    /// </summary>
    void WriteLine(string format, params object[] args);
}
