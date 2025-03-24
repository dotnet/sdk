// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.Tests;

internal class DebugTestOutputHelper : ITestOutputHelper
{
    public void WriteLine(string message)
    {
        Debug.WriteLine(message);
    }

    public void WriteLine(string format, params object[] args)
    {
        Debug.WriteLine(format, args);
    }
}
