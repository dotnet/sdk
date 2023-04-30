// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public static class CurrentFile
{
    public static string Path([CallerFilePath] string file = "") => file;

    public static string Relative(string relative, [CallerFilePath] string file = "") {
        return global::System.IO.Path.Combine(global::System.IO.Path.GetDirectoryName(file)!, relative); // file known to be not-null due to the mechanics of CallerFilePath
    }
}
