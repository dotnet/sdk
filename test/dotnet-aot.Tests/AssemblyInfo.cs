// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Tests in this assembly modify process-global state (environment variables,
// Reporter.Output, Console.Out/Error, AppContext data), so they must not
// run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
