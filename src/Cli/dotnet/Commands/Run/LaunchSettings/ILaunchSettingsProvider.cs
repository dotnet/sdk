// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;

internal interface ILaunchSettingsProvider
{
    LaunchSettingsApplyResult TryGetLaunchSettings(string? launchProfileName, JsonElement model);
}
