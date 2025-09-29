﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Analyzer.Utilities
{
    internal static class RoslynString
    {
        /// <inheritdoc cref="string.IsNullOrEmpty(string)"/>
        public static bool IsNullOrEmpty([NotNullWhen(returnValue: false)] string? value)
            => string.IsNullOrEmpty(value);

#if !NET20
        /// <inheritdoc cref="string.IsNullOrWhiteSpace(string)"/>
        public static bool IsNullOrWhiteSpace([NotNullWhen(returnValue: false)] string? value)
            => string.IsNullOrWhiteSpace(value);
#endif
    }
}
