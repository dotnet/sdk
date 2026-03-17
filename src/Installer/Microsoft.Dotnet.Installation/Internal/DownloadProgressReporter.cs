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
        _task.Description = $"{description} ({ProgressFormatting.FormatMB(0)} / {ProgressFormatting.FormatMB(0)})";
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
        _task.Description = $"{_description} ({ProgressFormatting.FormatMB(value.BytesDownloaded)} / {ProgressFormatting.FormatMB(total)})";
    }
}
