// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

[Flags]
public enum DotNetActions
{
    None = 0,
    Build = 1,
    Run = 2,
    RunWeb = 4,
    Publish = 8,
    PublishSelfContained = 16,
    PublishComplex = 32,
    PublishR2R = 64,
    Test = 128,
}
