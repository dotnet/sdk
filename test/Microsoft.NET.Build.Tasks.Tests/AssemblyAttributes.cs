// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Several tests in this assembly mutate the process-wide current working directory to verify
// that SDK tasks resolve relative paths through TaskEnvironment instead of Environment.CurrentDirectory
// (see Given*MultiThreading*.cs and GivenATaskEnvironmentDefault.cs). Disable xUnit parallelization
// at the assembly level so concurrent tests do not observe each other's CWD writes.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
