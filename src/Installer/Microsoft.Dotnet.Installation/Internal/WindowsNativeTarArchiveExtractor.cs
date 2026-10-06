// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.Dotnet.Installation.Internal;

internal sealed class WindowsNativeTarArchiveExtractor : ITarArchiveExtractor
{
    private const string TarExecutable = "tar.exe";
    private const string ExtractGzipArchiveArgument = "-xzf";
    private const string ExtractTarArchiveArgument = "-xf";
    private const string ChangeDirectoryArgument = "-C";

    private readonly ITarArchiveExtractor _fallbackExtractor;
    private readonly INativeTarProcessRunner _processRunner;

    public WindowsNativeTarArchiveExtractor(
        ITarArchiveExtractor fallbackExtractor,
        INativeTarProcessRunner? processRunner = null)
    {
        _fallbackExtractor = fallbackExtractor;
        _processRunner = processRunner ?? new NativeTarProcessRunner();
    }

    public void Extract(TarExtractionContext context)
    {
        string stagingDirectory = CreateStagingDirectoryPath(context.TargetDirectory);
        string tarExecutable = GetTarExecutable(
            Environment.SystemDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess);
        Exception? nativeFailure = null;

        try
        {
            Directory.CreateDirectory(context.TargetDirectory);
            Directory.CreateDirectory(stagingDirectory);

            // -x extracts from the archive named by -f; -z enables gzip decompression.
            // -C changes to the staging directory before writing archive entries.
            string extractArgument = context.ArchivePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                ? ExtractGzipArchiveArgument
                : ExtractTarArchiveArgument;
            NativeTarProcessResult result = _processRunner.Run(
                tarExecutable,
                [extractArgument, context.ArchivePath, ChangeDirectoryArgument, stagingDirectory]);
            if (result.StartFailure is not null)
            {
                nativeFailure = new InvalidOperationException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.NativeTarStartFailed,
                        tarExecutable,
                        result.StartFailure.Message),
                    result.StartFailure);
                ReportFallback(nativeFailure.Message);
                TryDeleteDirectory(stagingDirectory);
                ExtractWithFallback(context, nativeFailure);
                return;
            }

            if (result.ExitCode != 0)
            {
                nativeFailure = new InvalidOperationException(CreateFailureMessage(
                    tarExecutable,
                    result.ExitCode!.Value,
                    result.StandardError,
                    context.ArchivePath,
                    stagingDirectory));
                ReportFallback(nativeFailure.Message);
                TryDeleteDirectory(stagingDirectory);
                ExtractWithFallback(context, nativeFailure);
                return;
            }

            StagedTarArchiveCommitter.Commit(stagingDirectory, context);
        }
        finally
        {
            TryDeleteDirectory(stagingDirectory);
        }
    }

    internal static string GetTarExecutable(string systemDirectory, string windowsDirectory, bool useSysnative)
    {
        string candidate = useSysnative
            ? Path.Combine(windowsDirectory, "Sysnative", TarExecutable)
            : Path.Combine(systemDirectory, TarExecutable);
        return File.Exists(candidate) ? candidate : TarExecutable;
    }

    private void ExtractWithFallback(TarExtractionContext context, Exception nativeFailure)
    {
        try
        {
            _fallbackExtractor.Extract(context);
        }
        catch (Exception fallbackFailure)
        {
            throw new AggregateException(
                Strings.NativeTarBothExtractorsFailed,
                nativeFailure,
                fallbackFailure);
        }
    }

    private static string CreateStagingDirectoryPath(string targetDirectory)
    {
        return Path.Combine(Path.GetFullPath(targetDirectory), $".dotnetup-staging-{Guid.NewGuid():N}");
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static void ReportFallback(string reason)
        => Console.Error.WriteLine(
            string.Format(CultureInfo.CurrentCulture, Strings.NativeTarFallbackWarning, reason));

    private static string CreateFailureMessage(
        string executable,
        int exitCode,
        string standardError,
        string archivePath,
        string destinationPath)
    {
        string details = string.IsNullOrWhiteSpace(standardError)
            ? string.Empty
            : string.Format(
                CultureInfo.CurrentCulture,
                Strings.NativeTarStandardErrorDetails,
                standardError.Trim());
        return string.Format(
            CultureInfo.CurrentCulture,
            Strings.NativeTarExtractionFailed,
            executable,
            exitCode,
            archivePath,
            destinationPath,
            details);
    }
}
