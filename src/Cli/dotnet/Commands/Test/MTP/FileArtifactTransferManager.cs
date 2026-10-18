// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed class FileArtifactTransferManager : IDisposable
{
    private readonly Lock _lock = new();
    private readonly string _moduleDirectory;
    private readonly Dictionary<(string Execution, string Instance, string Transfer), Transfer> _pending = [];
    private readonly HashSet<(string Execution, string Instance, string Transfer)> _completed = [];
    private readonly HashSet<string> _publishedPaths = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly Action<string> _reportFailure;
    private long _reservedBytes;
    private bool _hasFailure;
    private bool _disposed;

    internal FileArtifactTransferManager(
        string resultsDirectory,
        FileArtifactTransferLimits limits,
        Action<string> reportFailure)
    {
        Limits = limits;
        _moduleDirectory = Path.Combine(Path.GetFullPath(resultsDirectory), "browser", Guid.NewGuid().ToString("N"));
        _reportFailure = reportFailure;
    }

    internal FileArtifactTransferLimits Limits { get; }

    internal bool HasIncompleteOrFailedTransfers
    {
        get
        {
            lock (_lock)
            {
                return _hasFailure || _pending.Count != 0;
            }
        }
    }

    internal bool OwnsPublishedPath(string? path)
    {
        lock (_lock)
        {
            return path is not null && _publishedPaths.Contains(path);
        }
    }

    internal FileArtifactTransferResponse Receive(FileArtifactTransferRequest request)
    {
        lock (_lock)
        {
            if (_disposed || !IsIdentifier(request.ExecutionId) || !IsIdentifier(request.InstanceId) ||
                !IsIdentifier(request.TransferId))
            {
                return Reject(CliCommandStrings.ArtifactTransferInvalidMetadata);
            }

            var key = (request.ExecutionId!, request.InstanceId!, request.TransferId!);
            if (request.TotalLength < 0 || request.Offset < 0 || request.Data is null ||
                request.Data.Length > FileArtifactTransferLimits.MaximumChunkBytes)
            {
                Abort(key);
                return Reject(CliCommandStrings.ArtifactTransferInvalidMetadata);
            }

            if (!IsFileName(request.FileName))
            {
                Abort(key);
                return Reject(CliCommandStrings.ArtifactTransferInvalidFileName);
            }

            try
            {
                if (!_pending.TryGetValue(key, out Transfer? transfer))
                {
                    if (_completed.Contains(key) || request.Offset != 0)
                    {
                        return Reject(CliCommandStrings.ArtifactTransferInvalidSequence);
                    }

                    if (request.TotalLength > Limits.MaximumFileBytes ||
                        request.TotalLength > Limits.MaximumModuleBytes - _reservedBytes ||
                        _pending.Count + _completed.Count >= Limits.MaximumFileCount)
                    {
                        return Reject(CliCommandStrings.ArtifactTransferLimitExceeded);
                    }

                    transfer = CreateTransfer(request.FileName!, request.TotalLength);
                    _pending.Add(key, transfer);
                    _reservedBytes += request.TotalLength;
                }

                if (transfer.FileName != request.FileName || transfer.TotalLength != request.TotalLength ||
                    transfer.Offset != request.Offset || request.Data.Length > transfer.TotalLength - transfer.Offset ||
                    (request.Data.Length == 0 && !request.IsFinal) ||
                    (request.IsFinal && request.Data.Length != transfer.TotalLength - transfer.Offset))
                {
                    Abort(key);
                    return Reject(CliCommandStrings.ArtifactTransferInvalidSequence);
                }

                transfer.Stream.Write(request.Data);
                transfer.Offset += request.Data.Length;
                if (!request.IsFinal)
                {
                    return new FileArtifactTransferResponse(transfer.Offset, null, null);
                }

                transfer.Stream.Dispose();
                File.Move(transfer.PartialPath, transfer.FullPath, overwrite: false);
                _pending.Remove(key);
                _completed.Add(key);
                _publishedPaths.Add(transfer.FullPath);
                return new FileArtifactTransferResponse(transfer.Offset, transfer.FullPath, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Abort(key);
                return Reject(string.Format(CliCommandStrings.ArtifactTransferIOError, ex.Message));
            }
        }
    }

    private Transfer CreateTransfer(string fileName, long length)
    {
        string directory = Path.Combine(_moduleDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string partialPath = Path.Combine(directory, ".partial");
        try
        {
            var stream = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            return new Transfer(fileName, length, directory, partialPath, Path.Combine(directory, fileName), stream);
        }
        catch
        {
            DeletePartial(partialPath, directory);
            throw;
        }
    }

    private void Abort((string Execution, string Instance, string Transfer) key)
    {
        if (_pending.Remove(key, out Transfer? transfer))
        {
            _reservedBytes -= transfer.TotalLength;
            try
            {
                transfer.Stream.Dispose();
            }
            catch (IOException ex)
            {
                ReportFailure(string.Format(CliCommandStrings.ArtifactTransferCleanupFailed, ex.Message));
            }

            DeletePartial(transfer.PartialPath, transfer.Directory);
        }
    }

    private void DeletePartial(string partialPath, string directory)
    {
        try
        {
            File.Delete(partialPath);
            Directory.Delete(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ReportFailure(string.Format(CliCommandStrings.ArtifactTransferCleanupFailed, ex.Message));
        }
    }

    private static bool IsIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= 128;

    private static bool IsFileName(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Length <= 255 &&
           value is not "." and not ".." &&
           !string.Equals(value, ".partial", StringComparison.OrdinalIgnoreCase) &&
           !value.EndsWith('.') && !value.EndsWith(' ') &&
           !value.Any(static c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c));

    private FileArtifactTransferResponse Reject(string error)
    {
        ReportFailure(error);
        return new(0, null, error);
    }

    private void ReportFailure(string error)
    {
        _hasFailure = true;
        _reportFailure(error);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var key in _pending.Keys.ToArray())
            {
                Abort(key);
            }

            if (_completed.Count == 0 && Directory.Exists(_moduleDirectory))
            {
                try
                {
                    Directory.Delete(_moduleDirectory);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    ReportFailure(string.Format(CliCommandStrings.ArtifactTransferCleanupFailed, ex.Message));
                }
            }
        }
    }

    private sealed class Transfer(
        string fileName, long totalLength, string directory, string partialPath, string fullPath, FileStream stream)
    {
        internal string FileName { get; } = fileName;
        internal long TotalLength { get; } = totalLength;
        internal string Directory { get; } = directory;
        internal string PartialPath { get; } = partialPath;
        internal string FullPath { get; } = fullPath;
        internal FileStream Stream { get; } = stream;
        internal long Offset { get; set; }
    }
}
