// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.CodeAnalysis.Tools
{
    [Flags]
    internal enum FixCategory
    {
        None = 0,
        Whitespace = 1,
        CodeStyle = 2,
        Analyzers = 4
    }
}
