// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install;

internal record ManifestUpdateWithWorkloads(ManifestVersionUpdate ManifestUpdate, WorkloadCollection Workloads);
