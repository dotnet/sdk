// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.NativeWrapper
{
#if INTERNALIZE_SHARED_TYPES
    internal
#else
    public
#endif
    interface INETBundleProvider
    {
        NetEnvironmentInfo GetDotnetEnvironmentInfo(string dotnetDir);
    }
}
