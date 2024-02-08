// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

public class PackageDiff: Microsoft.Build.Utilities.ToolTask
{
    [Required]
    public string BaselinePackage {get; set;} = "";

    [Required]
    public string TestPackage {get; set;} = "";

    protected override string ToolName { get; } = $"PackageDiff" + (System.Environment.OSVersion.Platform == PlatformID.Unix ? "" : ".exe");

    protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.High;
    protected override bool HandleTaskExecutionErrors() => true;

    protected override string GenerateFullPathToTool()
    {
        return Path.Combine(Path.GetDirectoryName(typeof(PackageDiff).Assembly.Location)!, "..", "..", "tools", ToolName);
    }

    protected override string GenerateCommandLineCommands()
    {
        return $"\"{BaselinePackage}\" \"{TestPackage}\"";
    }
}
