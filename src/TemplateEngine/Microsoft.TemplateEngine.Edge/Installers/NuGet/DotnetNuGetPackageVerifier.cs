// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.TemplateEngine.CommandUtils;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    internal interface INuGetPackageVerifier
    {
        bool IsEnabled { get; }

        Task VerifyAsync(string packagePath, SourceRepository repository, ISettings settings, ILogger logger, CancellationToken cancellationToken);
    }

    internal sealed class DotnetNuGetPackageVerifier : INuGetPackageVerifier
    {
        private const string SignatureVerificationEnvironmentVariable = "DOTNET_NUGET_SIGNATURE_VERIFICATION";

        public DotnetNuGetPackageVerifier()
        {
            bool? configuredValue = bool.TryParse(
                Environment.GetEnvironmentVariable(SignatureVerificationEnvironmentVariable),
                out bool shouldVerify)
                    ? shouldVerify
                    : null;

            IsEnabled = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                || configuredValue == true
                || (configuredValue == null && RuntimeInformation.IsOSPlatform(OSPlatform.Linux));
        }

        public bool IsEnabled { get; }

        public async Task VerifyAsync(string packagePath, SourceRepository repository, ISettings settings, ILogger logger, CancellationToken cancellationToken)
        {
            _ = packagePath ?? throw new ArgumentNullException(nameof(packagePath));
            _ = repository ?? throw new ArgumentNullException(nameof(repository));
            _ = settings ?? throw new ArgumentNullException(nameof(settings));
            _ = logger ?? throw new ArgumentNullException(nameof(logger));
            cancellationToken.ThrowIfCancellationRequested();

            bool isSigned;
            using (var reader = new PackageArchiveReader(packagePath))
            {
                isSigned = await reader.IsSignedAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!isSigned)
            {
                ClientPolicyContext clientPolicy = ClientPolicyContext.GetClientPolicy(settings, logger);
                RepositorySignatureResource? repositorySignature =
                    await repository.GetResourceAsync<RepositorySignatureResource>(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (clientPolicy.Policy == SignatureValidationMode.Require || repositorySignature?.AllRepositorySigned == true)
                {
                    const string message = "The package is unsigned, but client or repository policy requires signing.";
                    logger.LogDebug(message);
                    throw new InvalidDataException(message);
                }

                logger.LogDebug("Accepting unsigned package under accept policy.");
                return;
            }

            await VerifySignedPackageAsync(packagePath, logger, cancellationToken).ConfigureAwait(false);
        }

        private static async Task VerifySignedPackageAsync(string packagePath, ILogger logger, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"nuget verify --all {ArgumentEscaper.EscapeSingleArg(packagePath)}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start 'dotnet nuget verify'.");
            }

#if NET
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            Task<string> standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);
#else
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
#endif
            await WaitForExitAsync(process, logger, cancellationToken).ConfigureAwait(false);

            string output = await standardOutput.ConfigureAwait(false);
            string error = await standardError.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            LogOutput(logger, output, LogLevel.Debug);
            LogOutput(logger, error, process.ExitCode == 0 ? LogLevel.Debug : LogLevel.Error);

            if (process.ExitCode != 0)
            {
                string command = $"{process.StartInfo.FileName} {process.StartInfo.Arguments}";
                throw new SecurityException($"'{command}' failed with exit code {process.ExitCode}.");
            }

            return;

            static void LogOutput(ILogger logger, string output, LogLevel level)
            {
                if (!string.IsNullOrWhiteSpace(output))
                {
                    logger.Log(level, output.Trim());
                }
            }
        }

        private static async Task WaitForExitAsync(Process process, ILogger logger, CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(() => TerminateProcess(process, logger)))
            {
                // Process.WaitForExitAsync is unavailable on netstandard2.0 and net472.
                await Task.Run(process.WaitForExit, CancellationToken.None).ConfigureAwait(false);
            }

            return;

            static void TerminateProcess(Process process, ILogger logger)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                    // The process exited between checking HasExited and calling Kill.
                }
                catch (Win32Exception ex)
                {
                    if (!process.HasExited)
                    {
                        logger.LogDebug($"Failed to terminate 'dotnet nuget verify': {ex}");
                    }
                }
            }
        }
    }
}
