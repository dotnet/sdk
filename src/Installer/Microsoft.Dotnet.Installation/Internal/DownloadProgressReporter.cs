// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

public class DownloadProgressReporter : IProgress<DownloadProgress>
{
    private readonly IProgressTask _task;
    private readonly string _description;
    private long? _totalBytes;

    public DownloadProgressReporter(IProgressTask task, string description)
    {
        _task = task;
        _description = description;
        // Pad the initial description so the row is the same width as when
        // download progress is being reported — prevents column jumping.
        _task.Description = description + new string(' ', InstallComponentExtensions.DownloadSuffixWidth);
    }

    public void Report(DownloadProgress value)
    {
        if (value.TotalBytes.HasValue)
        {
            _totalBytes = value.TotalBytes;
        }
        if (_totalBytes.HasValue && _totalBytes.Value > 0)
        {
            double percent = (double)value.BytesDownloaded / _totalBytes.Value * 100.0;
            _task.Value = percent;
            // Fixed-width: "( nnn.n MB / nnn.n MB)" — always 22 chars
            _task.Description = $"{_description} ({FormatMB(value.BytesDownloaded)} / {FormatMB(_totalBytes.Value)})";
        }
        else
        {
            _task.Description = $"{_description} ({FormatMB(value.BytesDownloaded)})";
        }
    }

    /// <summary>
    /// Formats bytes as MB, right-aligned to 8 characters (e.g. "  0.7 MB", "290.4 MB").
    /// Always uses MB so the unit width is consistent across all progress rows.
    /// </summary>
    private static string FormatMB(long bytes)
    {
        return FormattableString.Invariant($"{bytes / (1024.0 * 1024.0),5:F1} MB");
    }
}
