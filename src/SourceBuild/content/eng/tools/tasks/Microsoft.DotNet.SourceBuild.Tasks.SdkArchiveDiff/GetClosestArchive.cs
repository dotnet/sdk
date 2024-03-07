// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

public abstract class GetClosestArchive : Microsoft.Build.Utilities.Task, ICancelableTask
{
    [Required]
    public required string BuiltArchivePath { get; init; }

    [Output]
    public string ClosestOfficialArchivePath { get; set; } = "";

    private string? _builtVersion;
    protected string BuiltVersion
    {
        get => _builtVersion ?? throw new InvalidOperationException();
        private set => _builtVersion = value;
    }

    private string? _builtRid;
    protected string BuiltRid
    {
        get => _builtRid ?? throw new InvalidOperationException();
        private set => _builtRid = value;
    }

    private string? _archiveExtension;
    protected string ArchiveExtension
    {
        get => _archiveExtension ?? throw new InvalidOperationException();
        private set => _archiveExtension = value;
    }

    /// <summary>
    /// The name of the package to find the closest official archive for. For example, "dotnet-sdk" or "aspnetcore-runtime".
    /// </summary>
    protected abstract string ArchiveName { get; }

    private CancellationTokenSource _cancellationTokenSource = new();
    protected CancellationToken CancellationToken => _cancellationTokenSource.Token;
    public void Cancel()
    {
        _cancellationTokenSource.Cancel();
    }

    /// <summary>
    /// Get the URL of the latest official archive for the given version string and RID.
    /// </summary>
    public abstract Task<string?> GetClosestOfficialArchiveUrl();

    public abstract Task<string?> GetClosestOfficialArchiveVersion();

    public override bool Execute()
    {
        return Task.Run(ExecuteAsync).Result;
    }

    public async Task<bool> ExecuteAsync()
    {
        CancellationToken.ThrowIfCancellationRequested();
        var filename = Path.GetFileName(BuiltArchivePath);
        (BuiltVersion, BuiltRid, ArchiveExtension) = Archive.GetInfoFromFileName(filename, ArchiveName);
        Log.LogMessage($"Finding closest official archive for '{ArchiveName}' version '{BuiltVersion}' RID '{BuiltRid}'");

        string? downloadUrl = await GetClosestOfficialArchiveUrl();
        if (downloadUrl == null)
        {
            Log.LogError($"Failed to find a download URL for '{ArchiveName}' version '{BuiltVersion}' RID '{BuiltRid}'");
            return false;
        }

        HttpClient client = new HttpClient();

        Log.LogMessage(MessageImportance.High, $"Downloading {downloadUrl}");
        HttpResponseMessage packageResponse = await client.GetAsync(downloadUrl, CancellationToken);

        var packageUriPath = packageResponse.RequestMessage!.RequestUri!.LocalPath;

        ClosestOfficialArchivePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + "." + Path.GetFileName(packageUriPath));
        Log.LogMessage($"Copying {packageUriPath} to {ClosestOfficialArchivePath}");
        using (var file = File.Create(ClosestOfficialArchivePath))
        {
            await packageResponse.Content.CopyToAsync(file, CancellationToken);
        }

        return true;
    }
}
