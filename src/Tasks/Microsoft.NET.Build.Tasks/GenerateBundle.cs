// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.HostModel.Bundle;

namespace Microsoft.NET.Build.Tasks
{
    public class GenerateBundle : TaskBase, ICancelableTask
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly Random _jitter =
#if NET
            Random.Shared;
#else
            new Random();
#endif

        [Required]
        public ITaskItem[] FilesToBundle { get; set; } = null!;
        [Required]
        public string AppHostName { get; set; } = null!;
        [Required]
        public bool IncludeSymbols { get; set; }
        [Required]
        public bool IncludeNativeLibraries { get; set; }
        [Required]
        public bool IncludeAllContent { get; set; }
        [Required]
        public string TargetFrameworkVersion { get; set; } = null!;
        [Required]
        public string RuntimeIdentifier { get; set; } = null!;
        [Required]
        public string OutputDir { get; set; } = null!;
        [Required]
        public bool ShowDiagnosticOutput { get; set; }
        [Required]
        public bool EnableCompressionInSingleFile { get; set; }
        public bool EnableMacOsCodeSign { get; set; } = true;

        [Output]
        public ITaskItem[] ExcludedFiles { get; set; } = null!;

        public int? RetryCount { get; set; } = 3;

        public void Cancel() => _cancellationTokenSource.Cancel();

        protected override void ExecuteCore()
        {
            ExecuteWithRetry().GetAwaiter().GetResult();
        }

        private async Task ExecuteWithRetry()
        {
            OSPlatform targetOS = RuntimeIdentifier.StartsWith("win") ? OSPlatform.Windows :
                                  RuntimeIdentifier.StartsWith("osx") ? OSPlatform.OSX :
                                  RuntimeIdentifier.StartsWith("freebsd") ? OSPlatform.Create("FREEBSD") :
                                  RuntimeIdentifier.StartsWith("illumos") ? OSPlatform.Create("ILLUMOS") :
                                  OSPlatform.Linux;

            Architecture targetArch = RuntimeIdentifier.EndsWith("-x64") || RuntimeIdentifier.Contains("-x64-") ? Architecture.X64 :
                                      RuntimeIdentifier.EndsWith("-x86") || RuntimeIdentifier.Contains("-x86-") ? Architecture.X86 :
                                      RuntimeIdentifier.EndsWith("-arm64") || RuntimeIdentifier.Contains("-arm64-") ? Architecture.Arm64 :
                                      RuntimeIdentifier.EndsWith("-arm") || RuntimeIdentifier.Contains("-arm-") ? Architecture.Arm :
#if !NETFRAMEWORK
                                      RuntimeIdentifier.EndsWith("-riscv64") || RuntimeIdentifier.Contains("-riscv64-") ? Architecture.RiscV64 :
                                      RuntimeIdentifier.EndsWith("-loongarch64") || RuntimeIdentifier.Contains("-loongarch64-") ? Architecture.LoongArch64 :
#endif
                                      throw new ArgumentException(nameof(RuntimeIdentifier));

            BundleOptions options = BundleOptions.None;
            options |= IncludeNativeLibraries ? BundleOptions.BundleNativeBinaries : BundleOptions.None;
            options |= IncludeAllContent ? BundleOptions.BundleAllContent : BundleOptions.None;
            options |= IncludeSymbols ? BundleOptions.BundleSymbolFiles : BundleOptions.None;
            options |= EnableCompressionInSingleFile ? BundleOptions.EnableCompression : BundleOptions.None;

            Version version = new(TargetFrameworkVersion);
            var bundler = new Bundler(
                AppHostName,
                OutputDir,
                options,
                targetOS,
                targetArch,
                version,
                ShowDiagnosticOutput,
                macosCodesign: EnableMacOsCodeSign);

            var fileSpec = new List<FileSpec>(FilesToBundle.Length);

            foreach (var item in FilesToBundle)
            {
                fileSpec.Add(new FileSpec(sourcePath: item.ItemSpec,
                                          bundleRelativePath: item.GetMetadata(MetadataKeys.RelativePath)));
            }

            // GenerateBundle has been throwing IOException intermittently in CI runs when accessing the singlefilehost binary specifically.
            // We hope that it's a Defender issue and that a quick retry will paper over the intermittent delay.
            await DoWithRetry(() => bundler.GenerateBundle(fileSpec));

            // Certain files are excluded from the bundle, based on BundleOptions.
            // For example:
            //    Native files and contents files are excluded by default.
            //    hostfxr and hostpolicy are excluded until singlefilehost is available.
            // Return the set of excluded files in ExcludedFiles, so that they can be placed in the publish directory.

            ExcludedFiles = FilesToBundle.Zip(fileSpec, (item, spec) => (spec.Excluded) ? item : null).Where(x => x != null).ToArray()!;
        }

        public async Task DoWithRetry(Action action)
        {
            bool triedOnce = false;
            while (RetryCount > 0 || !triedOnce)
            {
                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    break;
                }
                try
                {
                    action();
                    break;
                }
                catch (IOException) when (RetryCount > 0)
                {
                    Log.LogMessage(MessageImportance.High, $"Unable to access file during bundling. Retrying {RetryCount} more times...");
                    RetryCount--;
                    await Task.Delay(_jitter.Next(10, 50));
                }
            }
        }
    }
}
