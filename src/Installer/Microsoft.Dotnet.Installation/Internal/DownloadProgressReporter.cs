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
        // Show placeholder MB values so the row has the same visible width as
        // rows that are actively downloading — trailing spaces get ignored by
        // Spectre's column layout, so we need real characters for alignment.
        _task.Description = $"{description} ({FormatMB(0)} / {FormatMB(0)})";
    }

    public void Report(DownloadProgress value)
    {
        if (value.TotalBytes.HasValue)
        {
            _totalBytes = value.TotalBytes;
        }
        long total = _totalBytes ?? 0;
        if (total > 0)
        {
            double percent = (double)value.BytesDownloaded / total * 100.0;
            _task.Value = percent;
        }
        // Always use the full two-value format to keep row width consistent
        _task.Description = $"{_description} ({FormatMB(value.BytesDownloaded)} / {FormatMB(total)})";
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
