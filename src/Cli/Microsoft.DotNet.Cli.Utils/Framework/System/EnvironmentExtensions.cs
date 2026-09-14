// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System;

internal static class EnvironmentExtensions
{
    extension(Environment)
    {
        /// <summary>
        ///  Returns the path of the executable that started the currently executing process.
        ///  Returns <see langword="null"/> when the path is not available.
        /// </summary>
        public static string? ProcessPath => Process.GetCurrentProcess().MainModule?.FileName;
    }
}
