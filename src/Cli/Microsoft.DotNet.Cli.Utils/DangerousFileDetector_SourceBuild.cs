// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils;

// Stub for source build, which never produces Windows binaries.
internal class DangerousFileDetector : IDangerousFileDetector
{
    public bool IsDangerous(string filePath) => false;
}
