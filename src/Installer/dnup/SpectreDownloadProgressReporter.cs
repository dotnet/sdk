using System;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper
{
    public class SpectreDownloadProgressReporter : IProgress<DownloadProgress>
    {
        private readonly ProgressTask _task;
        private readonly string _description;
        private long? _totalBytes;

        public SpectreDownloadProgressReporter(ProgressTask task, string description)
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

        private static string FormatBytes(long bytes)
        {
            if (bytes > 1024 * 1024)
                return $"{bytes / (1024 * 1024)} MB";
            if (bytes > 1024)
                return $"{bytes / 1024} KB";
            return $"{bytes} B";
        }
    }
}
