// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// This task is sourced from https://github.com/microsoft/msbuild/blob/04e508c36f9c1fe826264aef7c26ffb8f16e9bdc/src/Tasks/DownloadFile.cs
// Contains further modifications in followup commits.
// It alleviates the problem of time outs on DownloadFile Task. We are not the version of msbuild that has this fix, hence we have to locally 
// build it to get rid of the issue. 

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Represents a task that can download a file.
    /// </summary>
    public sealed class DownloadFileSB : BuildTask, ICancelableTask
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Gets or sets an optional filename for the destination file.  By default, the filename is derived from the <see cref="SourceUrl"/> if possible.
        /// </summary>
        public ITaskItem DestinationFileName { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="ITaskItem"/> that specifies the destination folder to download the file to.
        /// </summary>
        [Required]
        public ITaskItem DestinationFolder { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="ITaskItem"/> that contains details about the downloaded file.
        /// </summary>
        [Output]
        public ITaskItem DownloadedFile { get; set; }

        /// <summary>
        /// Gets or sets an optional number of times to retry if possible.
        /// </summary>
        public int Retries { get; set; }

        /// <summary>
        /// Gets or sets the number of milliseconds to wait before retrying.
        /// </summary>
        public int RetryDelayMilliseconds { get; set; } = 5 * 1000;

        /// <summary>
        /// Gets or sets an optional value indicating whether or not the download should be skipped if the file is up-to-date.
        /// </summary>
        public bool SkipUnchangedFiles { get; set; } = true;

        /// <summary>
        /// Gets or sets the URL to download.
        /// </summary>
        [Required]
        public string SourceUrl { get; set; }

        /// <summary>
        /// Gets or sets the timeout for a successful download. If exceeded, the download continues
        /// for another two timeout durations before failing. This makes it sometimes possible to
        /// determine whether the timeout is just a little too short, or if the download would never
        /// have finished.
        /// </summary>
        public string TimeoutSeconds { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="HttpMessageHandler"/> to use.  This is used by unit tests to mock a connection to a remote server.
        /// </summary>
        internal HttpMessageHandler HttpMessageHandler { get; set; }

        /// <inheritdoc cref="ICancelableTask.Cancel"/>
        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        private async Task<bool> ExecuteAsync()
        {
            if (!Uri.TryCreate(SourceUrl, UriKind.Absolute, out Uri uri))
            {
                Log.LogError($"DownloadFileSB.ErrorInvalidUrl {SourceUrl}");
                return false;
            }

            int retryAttemptCount = 0;

            CancellationToken cancellationToken = _cancellationTokenSource.Token;

            var startTime = DateTime.UtcNow;

            // Use the same API for the "success timeout" and the "would it ever succeed" timeout.
            var timeout = TimeSpan.Zero;
            var successCancellationTokenSource = new CancellationTokenSource();

            if (double.TryParse(TimeoutSeconds, out double timeoutSeconds))
            {
                timeout = TimeSpan.FromSeconds(timeoutSeconds);
                Log.LogMessage(MessageImportance.High, $"DownloadFileSB timeout set to {timeout}");

                successCancellationTokenSource.CancelAfter(timeout);
                _cancellationTokenSource.CancelAfter((int)(timeout.TotalMilliseconds * 3));
            }

            while (true)
            {
                try
                {
                    await DownloadAsync(uri, cancellationToken);
                    break;
                }
                catch (OperationCanceledException e) when (e.CancellationToken == cancellationToken)
                {
                    // This task is being cancelled. Exit the loop.
                    break;
                }
                catch (Exception e)
                {
                    bool canRetry = IsRetriable(e, out Exception actualException) && retryAttemptCount++ < Retries;

                    if (canRetry)
                    {
                        Log.LogWarning($"DownloadFileSB.Retrying {SourceUrl} {retryAttemptCount + 1} {RetryDelayMilliseconds} {actualException}");

                        try
                        {
                            await Task.Delay(RetryDelayMilliseconds, cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException delayException) when (delayException.CancellationToken == cancellationToken)
                        {
                            // This task is being cancelled, exit the loop
                            break;
                        }
                    }
                    else
                    {
                        Log.LogError($"DownloadFileSB.ErrorDownloading {SourceUrl} {actualException}");
                        break;
                    }
                }
            }

            var finishTime = DateTime.UtcNow;

            if (successCancellationTokenSource.IsCancellationRequested)
            {
                string error = $"{TimeoutSeconds} second timeout exceeded";

                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    error +=
                        $", but download completed after {finishTime - startTime}. " +
                        $"Try increasing timeout from {TimeoutSeconds} if this is acceptable.";
                }
                else
                {
                    error +=
                        $", and didn't complete within leeway after {finishTime - startTime}. " +
                        $"The download was likely never going to terminate. Investigate logs and " +
                        $"add additional logging if necessary.";
                }

                Log.LogError(error);
            }
            else
            {
                Log.LogMessage(
                    MessageImportance.High,
                    $"DownloadFileSB.Downloading Complete! Elapsed: {finishTime - startTime}");
            }

            return !_cancellationTokenSource.IsCancellationRequested && !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Attempts to download the file.
        /// </summary>
        /// <param name="uri">The parsed <see cref="Uri"/> of the request.</param>
        private async Task DownloadAsync(Uri uri, CancellationToken cancellationToken)
        {
            // The main reason to use HttpClient vs WebClient is because we can pass a message handler for unit tests to mock
            using (var client = new HttpClient(HttpMessageHandler ?? new HttpClientHandler(), disposeHandler: true))
            {
                // Only get the response without downloading the file so we can determine if the file is already up-to-date
                using (HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    try
                    {
                        response.EnsureSuccessStatusCode();
                    }
                    catch (HttpRequestException e)
                    {
                        // HttpRequestException does not have the status code so its wrapped and thrown here so that later on we can determine
                        // if a retry is possible based on the status code
                        throw new CustomHttpRequestException(e.Message, e.InnerException, response.StatusCode);
                    }

                    if (!TryGetFileName(response, out string filename))
                    {
                        Log.LogError($"DownloadFileSB.ErrorUnknownFileName {SourceUrl} {nameof(DestinationFileName)}");
                        return;
                    }

                    DirectoryInfo destinationDirectory = Directory.CreateDirectory(DestinationFolder.ItemSpec);

                    var destinationFile = new FileInfo(Path.Combine(destinationDirectory.FullName, filename));

                    // The file is considered up-to-date if its the same length.  This could be inaccurate, we can consider alternatives in the future
                    if (ShouldSkip(response, destinationFile))
                    {
                        Log.LogMessage(MessageImportance.Normal, $"DownloadFileSB.DidNotDownloadBecauseOfFileMatch {SourceUrl}", destinationFile.FullName, nameof(SkipUnchangedFiles), "true");

                        DownloadedFile = new TaskItem(destinationFile.FullName);

                        return;
                    }

                    var progressMonitorCancellationTokenSource = new CancellationTokenSource();
                    CancellationToken progressMonitorToken = progressMonitorCancellationTokenSource.Token;

                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var startTime = DateTime.UtcNow;

                        var progressMonitor = Task.Run(
                            async () =>
                            {
                                while (!progressMonitorToken.IsCancellationRequested)
                                {
                                    destinationFile.Refresh();
                                    if (destinationFile.Exists)
                                    {
                                        long current = destinationFile.Length;
                                        long total = response.Content.Headers.ContentLength ?? 1;
                                        var elapsed = DateTime.UtcNow - startTime;
                                        double kbytesPerSecond = current / elapsed.TotalSeconds / 1000.0;

                                        Log.LogMessage(
                                            MessageImportance.High,
                                            $"Progress... {elapsed}, " +
                                            $"current file size {current / (double)total:00.0%} " +
                                            $"({destinationFile.Length:#,0} / {total:#,0}) " +
                                            $"~ {kbytesPerSecond:#,0.00} kB/s");
                                    }
                                    await Task.Delay(TimeSpan.FromSeconds(5), progressMonitorToken);
                                }
                            },
                            progressMonitorToken)
                            .ConfigureAwait(false);

                        using (var target = new FileStream(destinationFile.FullName, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            Log.LogMessage(
                                MessageImportance.High,
                                $"DownloadFileSB.Downloading {SourceUrl} to " +
                                $"{destinationFile.FullName}");

                            Log.LogMessage( MessageImportance.Low, $"All response headers:\n{response.Headers}");
                            Log.LogMessage( MessageImportance.Low, $"All content headers:\n{response.Content.Headers}");

                            using (Stream responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            {
                                await responseStream.CopyToAsync(target, 1024, cancellationToken).ConfigureAwait(false);
                            }

                            Log.LogMessage(MessageImportance.High, $"DownloadFileSB.StreamCopyComplete {SourceUrl}");

                            DownloadedFile = new TaskItem(destinationFile.FullName);
                        }
                    }
                    finally
                    {
                        if (DownloadedFile == null)
                        {
                            // Delete the file if anything goes wrong during download.  This could be destructive but we don't want to leave
                            // partially downloaded files on disk either.  Alternatively we could download to a temporary location and copy
                            // on success but we are concerned about the added I/O
                            destinationFile.Delete();
                        }

                        progressMonitorCancellationTokenSource.Cancel();
                    }
                }
            }
        }

        /// <summary>
        /// Determines if the specified exception is considered retriable.
        /// </summary>
        /// <param name="exception">The originally thrown exception.</param>
        /// <param name="actualException">The actual exception to be used for logging errors.</param>
        /// <returns><code>true</code> if the exception is retriable, otherwise <code>false</code>.</returns>
        private static bool IsRetriable(Exception exception, out Exception actualException)
        {
            actualException = exception;

            // Get aggregate inner exception
            if (actualException is AggregateException aggregateException && aggregateException.InnerException != null)
            {
                actualException = aggregateException.InnerException;
            }

            // Some HttpRequestException have an inner exception that has the real error
            if (actualException is HttpRequestException httpRequestException && httpRequestException.InnerException != null)
            {
                actualException = httpRequestException.InnerException;

                // An IOException inside of a HttpRequestException means that something went wrong while downloading
                if (actualException is IOException)
                {
                    return true;
                }
            }

            if (actualException is CustomHttpRequestException customHttpRequestException)
            {
                // A wrapped CustomHttpRequestException has the status code from the error
                switch (customHttpRequestException.StatusCode)
                {
                    case HttpStatusCode.InternalServerError:
                    case HttpStatusCode.RequestTimeout:
                        return true;
                }
            }

            if (actualException is WebException webException)
            {
                // WebException is thrown when accessing the Content of the response
                switch (webException.Status)
                {
                    // Don't retry on anything that cannot be compensated for
                    case WebExceptionStatus.TrustFailure:
                    case WebExceptionStatus.MessageLengthLimitExceeded:
                    case WebExceptionStatus.RequestProhibitedByCachePolicy:
                    case WebExceptionStatus.RequestProhibitedByProxy:
                        return false;

                    default:
                        // Retry on all other WebExceptions
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to get the file name to use when downloading the file.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponseMessage"/> with information about the response.</param>
        /// <param name="filename">Receives the name of the file.</param>
        /// <returns><code>true</code> if a file name could be determined, otherwise <code>false</code>.</returns>
        private bool TryGetFileName(HttpResponseMessage response, out string filename)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            // Not all URIs contain a file name so users will have to specify one
            // Example: http://www.download.com/file/1/

            filename = !String.IsNullOrWhiteSpace(DestinationFileName?.ItemSpec)
                ? DestinationFileName.ItemSpec // Get the file name from what the user specified
                : response.Content?.Headers?.ContentDisposition?.FileName // Attempt to get the file name from the content-disposition header value
                  ?? Path.GetFileName(response.RequestMessage.RequestUri.LocalPath); // Otherwise attempt to get a file name from the URI

            return !String.IsNullOrWhiteSpace(filename);
        }

        /// <summary>
        /// Represents a wrapper around the <see cref="HttpRequestException"/> that also contains the <see cref="HttpStatusCode"/>.
        /// </summary>
        private sealed class CustomHttpRequestException : HttpRequestException
        {
            public CustomHttpRequestException(string message, Exception inner, HttpStatusCode statusCode)
                : base(message, inner)
            {
                StatusCode = statusCode;
            }

            public HttpStatusCode StatusCode { get; }
        }

        private bool ShouldSkip(HttpResponseMessage response, FileInfo destinationFile)
        {
            return SkipUnchangedFiles
                   && destinationFile.Exists
                   && destinationFile.Length == response.Content.Headers.ContentLength
                   && response.Content.Headers.LastModified.HasValue
                   && destinationFile.LastWriteTimeUtc > response.Content.Headers.LastModified.Value.UtcDateTime;
        }
    }
}
