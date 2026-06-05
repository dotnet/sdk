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

/// <summary>
/// Collection definition for lifecycle tests (install, uninstall, global.json) that allows tests to run in parallel.
/// </summary>
[CollectionDefinition("DotnetupLifecycleCollection", DisableParallelization = false)]
public class DotnetupLifecycleCollection
{
    // This class has no code, and is never created. Its purpose is to be the place to apply
    // [CollectionDefinition] and all the collection settings.
}

/// <summary>
/// Serialized collection for tests that mutate process-wide environment variables
/// (e.g. DOTNET_NOLOGO). Tests in this collection run sequentially to avoid races.
/// </summary>
[CollectionDefinition("DotnetupEnvironmentMutationTests", DisableParallelization = true)]
public class DotnetupEnvironmentMutationTests
{
}

/// <summary>
/// Serialized collection for tests that mutate process-wide telemetry state
/// (e.g. Metrics.OnTrackEvent). Tests in this collection run sequentially to avoid races.
/// </summary>
[CollectionDefinition("DotnetupTelemetryStateMutationTests", DisableParallelization = true)]
public class DotnetupTelemetryStateMutationTests
{
}

/// <summary>
/// Serialized collection for tests that launch the real dotnetup executable as a child
/// process with redirected stdin/stdout. These tests are timing-sensitive (process startup,
/// PATH lookup, pipe drain) and have shown intermittent flakes when run concurrently with
/// other CPU-heavy or filesystem-heavy tests. Serializing them removes that contention.
/// </summary>
[CollectionDefinition("DotnetupProcessLaunchTests", DisableParallelization = true)]
public class DotnetupProcessLaunchTests
{
}
