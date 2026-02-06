// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal class SignCheck
{
    // Does not apply to Unix.
    public static bool IsDotNetSigned() => false;
}
