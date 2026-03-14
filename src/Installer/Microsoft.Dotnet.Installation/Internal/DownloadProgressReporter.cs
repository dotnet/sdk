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
            _task.Description = $"{_description} ({FormatBytes(value.BytesDownloaded)} / {FormatBytes(_totalBytes.Value)})";
        }
        else
        {
            _task.Description = $"{_description} ({FormatBytes(value.BytesDownloaded)})";
        }
    }

    /// <summary>
    /// Formats bytes as a right-aligned string so columns line up across progress rows.
    /// Output is always 8 characters wide (e.g. " 24.2 MB", "290.4 MB").
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        if (bytes > 1024 * 1024)
        {
            return FormattableString.Invariant($"{bytes / (1024.0 * 1024.0),5:F1} MB");
        }

        if (bytes > 1024)
        {
            return FormattableString.Invariant($"{bytes / 1024.0,5:F1} KB");
        }

        return FormattableString.Invariant($"{bytes,5} B");
    }
}
