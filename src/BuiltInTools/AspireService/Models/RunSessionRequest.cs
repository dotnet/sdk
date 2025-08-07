﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.WebTools.AspireServer.Contracts;

namespace Microsoft.WebTools.AspireServer.Models;

internal class EnvVar
{
    [Required]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

internal class LaunchConfiguration
{
    [Required]
    [JsonPropertyName("type")]
    public string LaunchType { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("project_path")]
    public string ProjectPath { get; set; } = string.Empty;

    [JsonPropertyName("launch_profile")]
    public string? LaunchProfile { get; set; }

    [JsonPropertyName("disable_launch_profile")]
    public bool DisableLaunchProfile { get; set; }

    [JsonPropertyName("mode")]
    public string LaunchMode { get; set; } = string.Empty;
}

internal class RunSessionRequest
{
    public const string Url = "/run_session";
    public const string VersionQuery = "api-version";
    public const string OurProtocolVersion = "2024-04-23";  // This means we support socket ping-pong keepalive
    public const string SupportedProtocolVersion = "2024-03-03";
    public const string ProjectLaunchConfigurationType = "project";
    public const string NoDebugLaunchMode = "NoDebug";
    public const string DebugLaunchMode = "Debug";

    [Required]
    [JsonPropertyName("launch_configurations")]
    public LaunchConfiguration[] LaunchConfigurations { get; set; } = Array.Empty<LaunchConfiguration>();

    [JsonPropertyName("env")]
    public EnvVar[] Environment { get; set; } = Array.Empty<EnvVar>();

    [JsonPropertyName("args")]
    public string[] Arguments { get; set; } = Array.Empty<string>();

    public ProjectLaunchRequest? ToProjectLaunchInformation()
    {
        // Only support one launch project request. Ignoring all others
        Debug.Assert(LaunchConfigurations.Length == 1, $"Unexpected number of launch configurations {LaunchConfigurations.Length}");

        var projectLaunchConfig = LaunchConfigurations.FirstOrDefault(launchConfig => string.Equals(launchConfig.LaunchType, ProjectLaunchConfigurationType, StringComparison.OrdinalIgnoreCase));
        if (projectLaunchConfig is null)
        {
            return null;
        }

        return new ProjectLaunchRequest()
        {
            ProjectPath = projectLaunchConfig.ProjectPath,
            Debug = string.Equals(projectLaunchConfig.LaunchMode, DebugLaunchMode, StringComparison.OrdinalIgnoreCase),
            Arguments = Arguments,
            Environment = Environment.Select(envVar => new KeyValuePair<string, string>(envVar.Name, envVar.Value!)),
            LaunchProfile = projectLaunchConfig.LaunchProfile,
            DisableLaunchProfile = projectLaunchConfig.DisableLaunchProfile
        };
    }
}
