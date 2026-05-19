// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

// Tests are resource-intensive (git clones, restores) and would contend for
// disk/network/CPU if run in parallel on a single agent.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
