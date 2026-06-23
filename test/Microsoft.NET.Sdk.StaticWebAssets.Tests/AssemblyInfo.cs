// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

// Tests in this assembly mutate process-global state (most notably the process
// current directory via Directory.SetCurrentDirectory), so they must not run in
// parallel. This preserves the behavior of the xUnit
// [assembly:CollectionBehavior(DisableTestParallelization = true)] attribute that
// existed before the migration to MSTest.
[assembly: DoNotParallelize]
