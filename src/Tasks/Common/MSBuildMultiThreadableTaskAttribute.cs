// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Polyfill for MSBuildMultiThreadableTaskAttribute for .NET Framework builds.
    /// This attribute is only recognized by newer MSBuild versions running on .NET.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class MSBuildMultiThreadableTaskAttribute : Attribute
    {
    }
}
#endif
