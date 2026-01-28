// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Collection for tests that capture Console.Out.
/// Tests in this collection run sequentially to avoid Console.Out conflicts.
/// </summary>
[CollectionDefinition("ConsoleCapture", DisableParallelization = true)]
public class ConsoleCaptureCollection
{
}
