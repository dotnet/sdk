using System;

namespace Microsoft.Dotnet.Installation
{
    /// <summary>
    /// Represents download progress information.
    /// </summary>
    public readonly struct DownloadProgress
    {
        /// <summary>
        /// Gets the number of bytes downloaded.
        /// </summary>
        public long BytesDownloaded { get; }

        /// <summary>
        /// Gets the total number of bytes to download, if known.
        /// </summary>
        public long? TotalBytes { get; }

        /// <summary>
        /// Gets the percentage of download completed, if total size is known.
        /// </summary>
        public double? PercentComplete => TotalBytes.HasValue ? (double)BytesDownloaded / TotalBytes.Value * 100 : null;

        public DownloadProgress(long bytesDownloaded, long? totalBytes)
        {
            BytesDownloaded = bytesDownloaded;
            TotalBytes = totalBytes;
        }
    }
}
