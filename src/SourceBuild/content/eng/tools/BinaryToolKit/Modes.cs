// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace BinaryToolKit;

[Flags]
public enum Modes
{
    Validate = 1,
    Clean = 2,
    All = Validate | Clean
}