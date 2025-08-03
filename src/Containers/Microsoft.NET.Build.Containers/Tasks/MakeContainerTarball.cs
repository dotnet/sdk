// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.MSBuild;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.NET.Build.Containers.Tasks;

public class MakeContainerTarball : Microsoft.Build.Utilities.Task, ICancelableTask
{
    private CancellationTokenSource _cts = new();

    [Required]
    public string ArchivePath { get; set; } = null!;

    [Required]
    public string Repository { get; set; } = string.Empty;

    [Required]
    public string[] Tags { get; set; } = [];

    [Required]
    public ITaskItem Manifest { get; set; } = null!;

    [Required]
    public ITaskItem Configuration { get; set; } = null!;

    [Required]
    public ITaskItem[] Layers { get; set; } = [];

    [Output]
    public string GeneratedArchiveFilePath { get; set; } = null!;


    public void Cancel() => _cts.Cancel();

    public override bool Execute() => ExecuteAsync().GetAwaiter().GetResult();

    public async Task<bool> ExecuteAsync()
    {
        using MSBuildLoggerProvider loggerProvider = new(Log);
        ILoggerFactory msbuildLoggerFactory = new LoggerFactory(new[] { loggerProvider });
        ILogger logger = msbuildLoggerFactory.CreateLogger<CreateImageIndex>();
        (long manifestSize, string manifestDigest, ManifestV2 manifestStructure) = await ReadManifest();
        var configDigest = manifestStructure.Config.Digest;
        var config = await Json.DeserializeAsync<Image>(File.OpenRead(Configuration.ItemSpec), cancellationToken: _cts.Token);
        var layers = Layers.Select(l => Layer.FromBackingFile(new(l.ItemSpec), GetDescriptor(l))).ToArray();
        var filePath = DetermineFilePath();
        await using var fileStream = File.Create(filePath);
        GeneratedArchiveFilePath = filePath;
        var telemetry = new Telemetry(new(null, null, null, Telemetry.LocalStorageType.Tarball), Log);
        await DockerCli.WriteImageToStreamAsync(Repository, Tags, config!, layers, manifestStructure, fileStream, _cts.Token);
        telemetry.LogPublishSuccess();
        return true;
    }

    private Descriptor GetDescriptor(ITaskItem item)
    {
        var mediaType = item.GetMetadata("MediaType");
        var digest = Digest.Parse(item.GetMetadata("Digest")!);
        var size = long.Parse(item.GetMetadata("Size")!);
        return new Descriptor
        {
            MediaType = mediaType,
            Digest = digest,
            Size = size
        };
    }

    private async Task<(long size, string digest, ManifestV2 manifest)> ReadManifest()
    {
        var size = long.Parse(Manifest.GetMetadata("Size")!);
        var digest = Manifest.GetMetadata("Digest")!;
        var manifestStructure = await Json.DeserializeAsync<ManifestV2>(File.OpenRead(Manifest.ItemSpec), cancellationToken: _cts.Token);
        return (size, digest, manifestStructure!);
    }

    private string DetermineFilePath()
    {

        var fullPath = Path.GetFullPath(ArchivePath);

        var directorySeparatorChar = Path.DirectorySeparatorChar;

        // if doesn't end with a file extension, assume it's a directory
        if (!Path.HasExtension(fullPath))
        {
            fullPath += Path.DirectorySeparatorChar;
        }

        // pointing to a directory? -> append default name
        if (fullPath.EndsWith(directorySeparatorChar))
        {
            fullPath = Path.Combine(fullPath, Repository + ".tar.gz");
        }

        // create parent directory if required.
        var parentDirectory = Path.GetDirectoryName(fullPath);
        if (parentDirectory != null && !Directory.Exists(parentDirectory))
        {
            Directory.CreateDirectory(parentDirectory);
        }

        return fullPath;
    }
}
