// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
