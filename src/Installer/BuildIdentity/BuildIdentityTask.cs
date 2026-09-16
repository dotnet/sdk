// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Build.Framework;

namespace Microsoft.Dotnet.BuildIdentity;

/// <summary>Generates and validates version metadata without executing the target artifact.</summary>
public sealed class BuildIdentityTask : Microsoft.Build.Utilities.Task
{
    [Required]
    public string Operation { get; set; } = "";
    [Required]
    public string ProductVersion { get; set; } = "";
    [Required]
    public string RuntimeIdentifier { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string ArtifactPath { get; set; } = "";
    public string SidecarPath { get; set; } = "";

    [Output]
    public string Identity { get; set; } = "";

    public override bool Execute()
    {
        try
        {
            switch (Operation)
            {
                case "generate":
                    Generate();
                    break;
                case "validate":
                    Validate();
                    break;
                default:
                    throw new InvalidDataException("Unsupported build identity operation: " + Operation);
            }

            return true;
        }
        catch (Exception exception)
        {
            Log.LogErrorFromException(exception, showStackTrace: false);
            return false;
        }
    }

    private void Generate()
    {
        Identity = BuildIdentityMetadata.Compute(ProductVersion, RuntimeIdentifier);
        BuildIdentityMetadata.WriteIfChanged(SourcePath, Encoding.UTF8.GetBytes(BuildIdentityMetadata.Source(ProductVersion, RuntimeIdentifier)));
    }

    private void Validate()
    {
        using var artifact = File.OpenRead(ArtifactPath);
        Identity = BuildIdentityMetadata.Validate(artifact, ProductVersion, RuntimeIdentifier);
        if (!string.IsNullOrEmpty(SidecarPath))
        {
            BuildIdentityMetadata.WriteIfChanged(SidecarPath, Encoding.ASCII.GetBytes(Identity + "\n"));
        }
    }
}