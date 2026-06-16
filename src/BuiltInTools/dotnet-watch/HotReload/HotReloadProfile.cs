// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch
{
    internal enum HotReloadProfile
    {
        Default,

        /// <summary>
        /// Blazor WebAssembly app
        /// </summary>
        BlazorWebAssembly,

        /// <summary>
        /// Blazor WebAssembly app hosted by an ASP.NET Core app.
        /// </summary>
        BlazorHosted,
    }
}
