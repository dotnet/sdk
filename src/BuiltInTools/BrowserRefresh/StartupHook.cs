// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

internal class StartupHook
{
    public static void Initialize()
    {
        // See https://github.com/dotnet/aspnetcore/issues/37357#issuecomment-941237000
        // We'll configure an environment variable that will indicate to blazor-wasm that the middleware is available.
        Environment.SetEnvironmentVariable("__ASPNETCORE_BROWSER_TOOLS", "true");
    }
}
