// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Manages progress reporting for the download and extraction phases of an installation.
/// Encapsulates task creation, description formatting, and completion updates so that
/// <see cref="DotnetArchiveExtractor"/> can focus on the download/extraction workflow.
/// </summary>
internal sealed class ExtractorProgressTracker
{
    private readonly IProgressReporter _reporter;
    private readonly InstallComponent _component;
    private readonly string _version;

    public ExtractorProgressTracker(IProgressReporter reporter, InstallComponent component, string version)
    {
        _reporter = reporter;
        _component = component;
        _version = version;
    }

    /// <summary>
    /// Creates a download progress task and returns a reporter that the downloader can use
    /// to report incremental progress.
    /// </summary>
    public (IProgress<DownloadProgress> Reporter, IProgressTask Task) BeginDownload()
    {
        string description = ProgressFormatting.FormatProgressDescription(ProgressFormatting.ActionDownloading, _component, _version);
        var task = _reporter.AddTask(description, 100);
        var reporter = new DownloadProgressReporter(task, description);
        return (reporter, task);
    }

    /// <summary>
    /// Marks the download task as complete, updating its description with the final file size.
    /// </summary>
    public void CompleteDownload(IProgressTask downloadTask, string archivePath)
    {
        downloadTask.Value = 100;
        long archiveBytes = new FileInfo(archivePath).Length;
        string downloadedDesc = ProgressFormatting.FormatProgressDescription(ProgressFormatting.ActionDownloaded, _component, _version);
        downloadTask.Description = $"{downloadedDesc} ({ProgressFormatting.FormatMB(archiveBytes)} / {ProgressFormatting.FormatMB(archiveBytes)})";
    }

    /// <summary>
    /// Creates an extraction progress task. The returned <see cref="IProgressTask"/> should be
    /// passed to the extraction methods so they can update it incrementally.
    /// </summary>
    public IProgressTask BeginExtraction()
    {
        string description = ProgressFormatting.FormatProgressDescription(ProgressFormatting.ActionInstalling, _component, _version);
        // Pad to match the width of "Downloading" rows (which have an MB suffix)
        // so all progress rows stay aligned within the shared Spectre column.
        string paddedDescription = description + new string(' ', ProgressFormatting.DownloadSuffixWidth);
        return _reporter.AddTask(paddedDescription, maxValue: 100);
    }

    /// <summary>
    /// Marks the extraction task as complete, switching its description to past tense.
    /// </summary>
    public void CompleteExtraction(IProgressTask extractionTask)
    {
        extractionTask.Description = ProgressFormatting.FormatProgressDescription(ProgressFormatting.ActionInstalled, _component, _version);
    }
}
