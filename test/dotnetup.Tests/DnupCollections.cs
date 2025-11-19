// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Collection definition that allows tests to run in parallel.
/// </summary>
[CollectionDefinition("DotnetupInstallCollection", DisableParallelization = false)]
public class DotnetupInstallCollection
{
    // This class has no code, and is never created. Its purpose is to be the place to apply
    // [CollectionDefinition] and all the collection settings.
}

/// <summary>
/// Collection definition for reuse tests that allows tests to run in parallel.
/// </summary>
[CollectionDefinition("DotnetupReuseCollection", DisableParallelization = false)]
public class DotnetupReuseCollection
{
    // This class has no code, and is never created. Its purpose is to be the place to apply
    // [CollectionDefinition] and all the collection settings.
}

/// <summary>
/// Collection definition for concurrency tests that allows tests to run in parallel.
/// </summary>
[CollectionDefinition("DotnetupConcurrencyCollection", DisableParallelization = false)]
public class DotnetupConcurrencyCollection
{
    // This class has no code, and is never created. Its purpose is to be the place to apply
    // [CollectionDefinition] and all the collection settings.
}
