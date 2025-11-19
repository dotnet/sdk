// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;

internal class ProjectLaunchProfileJson
{
    [JsonPropertyName("commandName")]
    public string? CommandName { get; set; }

    [JsonPropertyName("commandLineArgs")]
    public string? CommandLineArgs { get; set; }

    [JsonPropertyName("launchBrowser")]
    public bool LaunchBrowser { get; set; }

    [JsonPropertyName("launchUrl")]
    public string? LaunchUrl { get; set; }

    [JsonPropertyName("applicationUrl")]
    public string? ApplicationUrl { get; set; }

    [JsonPropertyName("dotnetRunMessages")]
    public bool DotNetRunMessages { get; set; }

    [JsonPropertyName("environmentVariables")]
    public Dictionary<string, string>? EnvironmentVariables { get; set; }
}
