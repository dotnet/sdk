// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.ComponentDetection.Detectors.NuGet;

#nullable disable

using static global::NuGet.Frameworks.FrameworkConstants.CommonFrameworks;

/// <summary>
/// Framework packages for .NETStandard,Version=v2.1.
/// </summary>
internal partial class FrameworkPackages
{
    internal static class NETStandard21
    {
        internal static FrameworkPackages Instance { get; } = new(NetStandard21, FrameworkNames.NetStandardLibrary, NETStandard20.Instance)
        {
            { "System.Buffers", "4.6.1" },
            { "System.Collections.Concurrent", "4.3.0" },
            { "System.ComponentModel", "4.3.0" },
            { "System.ComponentModel.EventBasedAsync", "4.3.0" },
            { "System.Diagnostics.Contracts", "4.3.0" },
            { "System.Dynamic.Runtime", "4.3.0" },
            { "System.Linq.Queryable", "4.3.0" },
            { "System.Memory", "4.6.3" },
            { "System.Net.Requests", "4.3.0" },
            { "System.Net.WebHeaderCollection", "4.3.0" },
            { "System.Numerics.Vectors", "4.6.1" },
            { "System.ObjectModel", "4.3.0" },
            { "System.Reflection.DispatchProxy", "4.5.1" },
            { "System.Reflection.Emit", "4.7.0" },
            { "System.Reflection.Emit.ILGeneration", "4.7.0" },
            { "System.Reflection.Emit.Lightweight", "4.7.0" },
            { "System.Runtime.Numerics", "4.3.0" },
            { "System.Runtime.Serialization.Json", "4.3.0" },
            { "System.Security.Principal", "4.3.0" },
            { "System.Threading", "4.3.0" },
            { "System.Threading.Tasks.Extensions", "4.6.3" },
            { "System.Threading.Tasks.Parallel", "4.3.0" },
            { "System.Xml.XDocument", "4.3.0" },
            { "System.Xml.XmlSerializer", "4.3.0" },
        };

        internal static void Register() => FrameworkPackages.Register(Instance);
    }
}
