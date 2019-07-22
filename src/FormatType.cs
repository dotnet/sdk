// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;

[Flags]
public enum FormatType
{
    None = 0,
    Whitespace = 1,
    CodeStyle = 2,
    All = Whitespace | CodeStyle
}
