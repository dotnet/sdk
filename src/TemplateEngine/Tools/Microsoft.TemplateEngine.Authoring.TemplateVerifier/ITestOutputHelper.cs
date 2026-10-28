// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.CommandUtils
{
    /// <summary>
    /// Runner-agnostic test output abstraction used by the shared
    /// <c>Microsoft.TemplateEngine.CommandUtils</c> sources that are compiled into this package.
    /// </summary>
    /// <remarks>
    /// The shared command sources reference the unqualified name <c>ITestOutputHelper</c>. In test
    /// projects that name binds to the framework-specific type (for example
    /// <c>Microsoft.NET.TestFramework.ITestOutputHelper</c> or xUnit v3's <c>ITestOutputHelper</c>)
    /// via a global using alias. This shipping package intentionally does not depend on any test
    /// framework, so it provides its own minimal binding here. The shape mirrors xUnit v3's
    /// <c>ITestOutputHelper</c> so the shared sources compile unchanged.
    /// </remarks>
    internal interface ITestOutputHelper
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
}
