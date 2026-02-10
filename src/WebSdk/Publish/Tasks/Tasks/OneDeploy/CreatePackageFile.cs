// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Sdk.Publish.Tasks.OneDeploy;

/// <summary>
/// A task that creates a package file for the content in a given path.
/// </summary>
public class CreatePackageFile : Task
{
    private readonly IFilePackager _filePackager;

    public CreatePackageFile()
    {
        _filePackager = new ZipFilePackager();
    }

    // Test constructor
    internal CreatePackageFile(IFilePackager filePackager)
    {
        _filePackager = filePackager;
    }

    [Required]
    public string ContentToPackage { get; set; }

    [Required]
    public string ProjectName { get; set; }

    [Required]
    public string IntermediateTempPath { get; set; }

    [Output]
    public string CreatedPackageFilePath { get; set; }

    /// <inheritdoc/>
    public override bool Execute()
    {
        if (string.IsNullOrEmpty(ContentToPackage)
            || string.IsNullOrEmpty(ProjectName)
            || string.IsNullOrEmpty(IntermediateTempPath))
        {
            return false;
        }

        var packageFileName = $"{ProjectName}-{DateTime.Now:yyyyMMddHHmmssFFF}{_filePackager.Extension}";
        var packageFilePath = Path.Combine(IntermediateTempPath, packageFileName);

        // package content
        var packageFileTask = _filePackager.CreatePackageAsync(ContentToPackage, packageFilePath, CancellationToken.None);
        packageFileTask.Wait();

        CreatedPackageFilePath = packageFileTask.Result ? packageFilePath : string.Empty;

        return !string.IsNullOrEmpty(CreatedPackageFilePath);
    }
}
