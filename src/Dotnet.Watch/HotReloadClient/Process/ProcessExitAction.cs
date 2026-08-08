// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Threading.Tasks;

namespace Microsoft.DotNet.HotReload;

internal delegate ValueTask ProcessExitAction(int processId, int? exitCode);
