// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.Dotnet.VersionMetadata;

/// <summary>Generates and validates version metadata without executing the target artifact.</summary>
public sealed class VersionMetadataTask : Microsoft.Build.Utilities.Task
{
    /// <summary>Gets or sets the operation: generate source or validate an artifact.</summary>
    [Required]
    public string Operation { get; set; } = "";
    /// <summary>Gets or sets the full product version.</summary>
    [Required]
    public string ProductVersion { get; set; } = "";
    /// <summary>Gets or sets the target runtime identifier.</summary>
    [Required]
    public string RuntimeIdentifier { get; set; } = "";
    /// <summary>Gets or sets the generated source file path.</summary>
    public string SourcePath { get; set; } = "";
    /// <summary>Gets or sets the artifact path to validate.</summary>
    public string ArtifactPath { get; set; } = "";
    /// <inheritdoc />
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
                    throw new InvalidDataException("Unsupported version metadata operation: " + Operation);
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
        VersionMetadataSource.WriteIfChanged(SourcePath, Encoding.UTF8.GetBytes(VersionMetadataSource.Source(ProductVersion, RuntimeIdentifier)));
    }

    private void Validate()
    {
        using var artifact = File.OpenRead(ArtifactPath);
        VersionMetadataSource.Validate(artifact, ProductVersion, RuntimeIdentifier);
    }
}