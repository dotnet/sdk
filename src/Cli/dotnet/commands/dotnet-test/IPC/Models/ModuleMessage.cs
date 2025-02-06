// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Test;

internal sealed record ModuleMessage(string? DllOrExePath, string? ProjectPath, string? TargetFramework, string? RunSettingsFilePath, string IsTestingPlatformApplication) : IRequest;
