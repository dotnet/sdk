
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.IO.Enumeration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Semver;
using Serde;
using Serde.Json;
using Spectre.Console;
using StaticCs;
using Zio;
using Zio.FileSystems;

namespace Dnvm;

/// <summary>
/// Deletes the given directory on disposal.
/// </summary>
public sealed record DirectoryResource(
    string Path,
    bool Recursive = true) : IDisposable
{
    public void Dispose()
    {
        Directory.Delete(Path, recursive: Recursive);
    }
}

public static class SpectreUtil
{
    public static Task<string?> DownloadWithProgress(
        this Logger logger,
        ScopedHttpClient client,
        string filePath,
        string url,
        string description,
        int? bufferSizeParam = null)
    {
        var console = logger.Console;
        return console.Progress()
            .AutoRefresh(true)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn())
            .StartAsync(async ctx =>
        {
            using var archiveResponse = await CancelScope.WithTimeoutAfter(DnvmEnv.DefaultTimeout,
                _ => client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead));
            if (!archiveResponse.IsSuccessStatusCode)
            {
                return await CancelScope.WithTimeoutAfter(DnvmEnv.DefaultTimeout,
                    _ => archiveResponse.Content.ReadAsStringAsync());
            }

            if (archiveResponse.Content.Headers.ContentLength is not { } contentLength)
            {
                throw new InvalidDataException("HTTP Content length is null");
            }

            // Use 1/100 of the file size as the buffer size, up to 1 MB.
            const int oneMb = 1024 * 1024;
            var bufferSize = bufferSizeParam ?? (int)Math.Min(contentLength / 100, oneMb);
            logger.Info($"Buffer size: {bufferSize} bytes");

            var progressTask = ctx.AddTask(description, maxValue: contentLength);
            console.MarkupLine($"Starting download (size: {contentLength / oneMb:0.00} MB)");

            using var tempArchiveFile = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024, // 64kB
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            using var archiveHttpStream = await CancelScope.WithTimeoutAfter(DnvmEnv.DefaultTimeout,
                _ => archiveResponse.Content.ReadAsStreamAsync());

            var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            while (true)
            {
                // We could have a timeout for downloading the file, but this is heavily dependent
                // on the user's download speed and if they suspend/resume the process. Instead
                // we'll rely on the user to cancel the download if it's taking too long.
                var read = await archiveHttpStream.ReadAsync(buffer);
                if (read == 0)
                {
                    break;
                }
                // Writing to disk shouldn't time out, but we'll check for cancellation
                await tempArchiveFile.WriteAsync(buffer.AsMemory(0, read), CancelScope.Current.Token);
                progressTask.Increment(read);
                ctx.Refresh();
            }
            await tempArchiveFile.FlushAsync(CancelScope.Current.Token);
            ArrayPool<byte>.Shared.Return(buffer);
            progressTask.StopTask();
            return null;
        });
    }
}

public static class Utilities
{
    /// <summary>
    /// Do our best to replace the target file with the source file. If the target file is locked,
    /// we will try to delete it and then copy the source file over. If the target file cannot be
    /// deleted, we will try to move it to the same folder with an additional ".old" suffix.
    /// </summary>
    public static void ForceReplaceFile(IFileSystem srcFs, UPath src, IFileSystem destFs, UPath dest)
    {
        try
        {
            // Does not throw if the file does not exist
            destFs.DeleteFile(dest);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            var destDir = dest.GetDirectory();
            var destName = dest.GetName();
            var destOld = destDir / (destName + ".old");
            destFs.MoveFile(dest, destOld);
        }
        srcFs.MoveFileCross(src, destFs, dest);
    }

    public static readonly string ZipSuffix = Environment.OSVersion.Platform == PlatformID.Win32NT ? ".zip" : ".tar.gz";

    [UnsupportedOSPlatform("windows")]
    public static void ChmodExec(string path)
    {
        var mod = File.GetUnixFileMode(path);
        File.SetUnixFileMode(path, mod | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }

    [UnsupportedOSPlatform("windows")]
    public static void ChmodExec(IFileSystem vfs, UPath upath)
    {
        var realPath = vfs.ConvertPathToInternal(upath);
        var mod = File.GetUnixFileMode(realPath);
        File.SetUnixFileMode(realPath, mod | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }

    public static string ToMajorMinor(this SemVersion version) => $"{version.Major}.{version.Minor}";

    public static string ToFeature(this SemVersion version)
    {
        int feature = version.Patch;
        while (feature >= 10)
        {
            feature /= 10;
        }

        return $"{version.Major}.{version.Minor}.{feature}xx";
    }

    public static string SeqToString<T>(this IEnumerable<T> e)
    {
        return "[ " + string.Join(", ", e) + " ]";
    }

    public static ImmutableArray<U> SelectAsArray<T, U>(this ImmutableArray<T> e, Func<T, U> f)
    {
        var builder = ImmutableArray.CreateBuilder<U>(e.Length);
        foreach (var item in e)
        {
            builder.Add(f(item));
        }
        return builder.MoveToImmutable();
    }

    public static EqArray<U> SelectAsArray<T, U>(this EqArray<T> e, Func<T, U> f)
    {
        var builder = ImmutableArray.CreateBuilder<U>(e.Length);
        foreach (var item in e)
        {
            builder.Add(f(item));
        }
        return new(builder.MoveToImmutable());
    }

    public static readonly RID CurrentRID = new RID(
        GetCurrentOSPlatform(),
        RuntimeInformation.OSArchitecture,
        RuntimeInformation.RuntimeIdentifier.Contains("musl") ? Libc.Musl : Libc.Default);

    private static OSPlatform GetCurrentOSPlatform()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OSPlatform.OSX :
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows :
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OSPlatform.Linux :
            throw new NotSupportedException("Current OS is not supported: " + RuntimeInformation.OSDescription);
    }

    [UnconditionalSuppressMessage("SingleFile", "IL3000", Justification = "Checks for empty location")]
    public static bool IsSingleFile => Assembly.GetExecutingAssembly()?.Location == "";

    public static string ProcessPath = Environment.ProcessPath
        ?? throw new InvalidOperationException("Cannot find exe name");

    public static string ExeSuffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? ".exe"
        : "";

    public static string DnvmExeName = "dnvm" + ExeSuffix;
    public static string DotnetExeName = "dotnet" + ExeSuffix;

    public static async Task<string?> ExtractArchiveToDir(string archivePath, string dirPath)
    {
        Directory.CreateDirectory(dirPath);
        if (Utilities.CurrentRID.OS != OSPlatform.Windows)
        {
            var procResult = await ProcUtil.RunWithOutput("tar", $"-xzf \"{archivePath}\" -C \"{dirPath}\"");
            return procResult.ExitCode == 0 ? null : procResult.Error;
        }
        else
        {
            try
            {
                ZipFile.ExtractToDirectory(archivePath, dirPath, overwriteFiles: true);
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
        return null;
    }

    public static async Task<string?> ExtractSdkToDir(
        SemVersion? existingMuxerVersion,
        SemVersion runtimeVersion,
        string archivePath,
        IFileSystem tempFs,
        IFileSystem destFs,
        UPath destDir)
    {
        destFs.CreateDirectory(destDir);
        var tempExtractDir = UPath.Root / Path.GetRandomFileName();
        tempFs.CreateDirectory(tempExtractDir);
        using var tempRealPath = new DirectoryResource(tempFs.ConvertPathToInternal(tempExtractDir));
        if (Utilities.CurrentRID.OS != OSPlatform.Windows)
        {
            var procResult = await ProcUtil.RunWithOutput("tar", $"-xzf \"{archivePath}\" -C \"{tempRealPath.Path}\"");
            if (procResult.ExitCode != 0)
            {
                return procResult.Error;
            }
        }
        else
        {
            try
            {
                ZipFile.ExtractToDirectory(archivePath, tempRealPath.Path, overwriteFiles: true);
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        try
        {
            // We want to copy over all the files from the extraction directory to the target
            // directory, with one exception: the top-level "dotnet exe" (muxer). That has special logic.
            CopyMuxer(existingMuxerVersion, runtimeVersion, tempFs, tempExtractDir, destFs, destDir);

            var extractFullName = tempExtractDir.FullName;
            foreach (var dir in tempFs.EnumerateDirectories(tempExtractDir))
            {
                destFs.CreateDirectory(destDir / dir.GetName());
                foreach (var fsItem in tempFs.EnumerateItems(dir, SearchOption.AllDirectories))
                {
                    var relativePath = fsItem.Path.FullName[extractFullName.Length..].TrimStart('/');
                    var destPath = destDir / relativePath;

                    if (fsItem.IsDirectory)
                    {
                        destFs.CreateDirectory(destPath);
                    }
                    else
                    {
                        ForceReplaceFile(tempFs, fsItem.Path, destFs, destPath);
                    }
                }
            }
        }
        catch (Exception e)
        {
            return e.Message;
        }
        return null;
    }

    private static void CopyMuxer(
        SemVersion? existingMuxerVersion,
        SemVersion newRuntimeVersion,
        IFileSystem tempFs,
        UPath tempExtractDir,
        IFileSystem destFs,
        UPath destDir)
    {   //The "dotnet" exe (muxer) is special in two ways:
        // 1. It is shared between all SDKs, so it may be locked by another process.
        // 2. It should always be the newest version, so we don't want to overwrite it if the SDK
        //    we're installing is older than the one already installed.
        //
        var muxerTargetPath = destDir / DotnetExeName;

        if (newRuntimeVersion.CompareSortOrderTo(existingMuxerVersion) <= 0)
        {
            // The new SDK is older than the existing muxer, so we don't need to do anything.
            return;
        }

        // The new SDK is newer than the existing muxer, so we need to replace it.
        ForceReplaceFile(tempFs, tempExtractDir / DotnetExeName, destFs, muxerTargetPath);
    }

    public static T Unwrap<T>(this T? t, [CallerArgumentExpression(parameterName: nameof(t))] string? expr = null) where T : class
    {
        if (t is null)
        {
            throw new NullReferenceException("Unexpected null value in expression: " + (expr ?? "(null)"));
        }
        return t;
    }
}

[Closed]
public enum Libc
{
    Default, // Not a real libc, refers to the most common platform libc
    Musl
}

public sealed record RID(
    OSPlatform OS,
    Architecture Arch,
    Libc Libc = Libc.Default)
{
    public override string ToString()
    {
        string os =
            OS == OSPlatform.Windows ? "win" :
            OS == OSPlatform.Linux   ? "linux" :
            OS == OSPlatform.OSX ? "osx" :
            throw new NotSupportedException("Unsupported OS: " + OS);

        string arch = Arch switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new NotSupportedException("Unsupported architecture")
        };
        return Libc switch
        {
            Libc.Default => string.Join("-", os, arch),
            Libc.Musl => string.Join('-', os, arch, "musl")
        };
    }
}
