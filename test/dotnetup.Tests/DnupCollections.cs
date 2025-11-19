// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.DotNet.Tools.Dnup.Tests;

/// <summary>
/// Collection definition that allows tests to run in parallel.
/// </summary>
[CollectionDefinition("DnupInstallCollection", DisableParallelization = false)]
public class DnupInstallCollection
{
    // This class has no code, and is never created. Its purpose is to be the place to apply
    // [CollectionDefinition] and all the collection settings.
}

/// <summary>
/// Collection definition for reuse tests that allows tests to run in parallel.
/// </summary>
[CollectionDefinition("DnupReuseCollection", DisableParallelization = false)]
public class DnupReuseCollection
{
    // This class has no code, and is never created. Its purpose is to be the place to apply
    // [CollectionDefinition] and all the collection settings.
}

/// <summary>
/// Collection definition for concurrency tests that allows tests to run in parallel.
/// </summary>
[CollectionDefinition("DnupConcurrencyCollection", DisableParallelization = false)]
public class DnupConcurrencyCollection
{
    // This class has no code, and is never created. Its purpose is to be the place to apply
    // [CollectionDefinition] and all the collection settings.
}
