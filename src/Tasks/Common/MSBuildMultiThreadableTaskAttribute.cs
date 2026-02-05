// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This is a polyfill for the MSBuildMultiThreadableTaskAttribute from MSBuild.
// MSBuild detects this attribute by its namespace and name only, ignoring the defining assembly.
// This allows us to use the attribute before the MSBuild version containing it is available.
// See: https://github.com/dotnet/msbuild/blob/main/src/Framework/MSBuildMultiThreadableTaskAttribute.cs

// This polyfill is only needed for .NET Framework builds since the newer MSBuild packages
// for .NET Core already include the attribute. When targeting .NET Core, we use the
// attribute from Microsoft.Build.Framework directly.

#if NETFRAMEWORK

#nullable enable

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Attribute that marks a task class as thread-safe for multithreaded execution.
    /// </summary>
    /// <remarks>
    /// Task classes marked with this attribute indicate they can be safely executed in parallel
    /// in the same process with other tasks.
    ///
    /// Tasks using this attribute must satisfy strict requirements:
    /// - Must not modify global process state (environment variables, working directory, etc.)
    /// - Must not depend on global process state, including relative path resolution
    ///
    /// MSBuild detects this attribute by its namespace and name only, ignoring the defining assembly.
    /// This allows customers to define the attribute in their own assemblies alongside their tasks.
    ///
    /// When defining polyfilled versions of this attribute in customer assemblies,
    /// they must also specify Inherited = false to ensure proper non-inheritable semantics.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class MSBuildMultiThreadableTaskAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the MSBuildMultiThreadableTaskAttribute class.
        /// </summary>
        public MSBuildMultiThreadableTaskAttribute()
        {
        }
    }
}

#endif
