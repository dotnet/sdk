// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.Build
{
    [MSBuildMultiThreadableTask]
    public class GenerateRuntimeAnalyzersSWR : Task, IMultiThreadableTask
    {
        /// <summary>
        /// Gets or sets the task environment for thread-safe operations.
        /// </summary>
        public TaskEnvironment? TaskEnvironment { get; set; }

        [Required]
        public string RuntimeAnalyzersLayoutDirectory { get; set; }

        [Required]
        public string OutputFile { get; set; }

        public override bool Execute()
        {
            StringBuilder sb = new StringBuilder(SWR_HEADER);

            // Ensure paths are absolute for thread-safe file operations
            string layoutDir = TaskEnvironment?.GetAbsolutePath(RuntimeAnalyzersLayoutDirectory) ?? RuntimeAnalyzersLayoutDirectory;
            string outputFile = TaskEnvironment?.GetAbsolutePath(OutputFile) ?? OutputFile;

            // NOTE: Keep in sync with SdkAnalyzerAssemblyRedirector.
            // This is intentionally short to avoid long path problems.
            const string installDir = @"Common7\IDE\CommonExtensions\Microsoft\DotNet";

            AddFolder(sb,
                      layoutDir,
                      "",
                      installDir,
                      filesToInclude:
                      [
                          "metadata.json",
                      ]);

            AddFolder(sb,
                      layoutDir,
                      "AspNetCoreAnalyzers",
                      @$"{installDir}\AspNetCoreAnalyzers");

            AddFolder(sb,
                      layoutDir,
                      "NetCoreAnalyzers",
                      @$"{installDir}\NetCoreAnalyzers");

            AddFolder(sb,
                      layoutDir,
                      "WindowsDesktopAnalyzers",
                      @$"{installDir}\WindowsDesktopAnalyzers");

            AddFolder(sb,
                      layoutDir,
                      "SDKAnalyzers",
                      @$"{installDir}\SDKAnalyzers");

            AddFolder(sb,
                      layoutDir,
                      "WebSDKAnalyzers",
                      @$"{installDir}\WebSDKAnalyzers");

            File.WriteAllText(outputFile, sb.ToString());

            return true;
        }

        private void AddFolder(StringBuilder sb, string layoutDirectory, string relativeSourcePath, string swrInstallDir, bool ngenAssemblies = false, IEnumerable<string> filesToInclude = null)
        {
            string sourceFolder = Path.Combine(layoutDirectory, relativeSourcePath);

            // If files were specified explicitly, check that they exist.
            if (filesToInclude != null)
            {
                foreach (var file in filesToInclude)
                {
                    var path = Path.Combine(sourceFolder, file);
                    if (!File.Exists(path))
                    {
                        throw new InvalidOperationException($"File not found: {path}");
                    }
                }
            }

            IEnumerable<string> files = filesToInclude ??
                Directory.GetFiles(sourceFolder)
                    .Where(static f =>
                    {
                        var extension = Path.GetExtension(f);
                        return !extension.Equals(".pdb", StringComparison.OrdinalIgnoreCase) &&
                            !extension.Equals(".swr", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(f).Equals("_._");
                    });

            if (files.Any())
            {
                sb.Append(@"folder ""InstallDir:\");
                sb.Append(swrInstallDir);
                sb.AppendLine(@"\""");

                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);

                    sb.Append(@"  file source=""$(PkgVS_Redist_Common_Net_Core_SDK_RuntimeAnalyzers)\");
                    sb.Append(Path.Combine(relativeSourcePath, fileName));
                    sb.Append('"');

                    if (ngenAssemblies && file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append(@" vs.file.ngenApplications=""[installDir]\Common7\IDE\vsn.exe""");
                    }

                    sb.AppendLine();
                }

                sb.AppendLine();
            }

            // Don't go to sub-folders if the list of files was explicitly specified.
            if (filesToInclude != null)
            {
                return;
            }

            foreach (var subfolder in Directory.GetDirectories(sourceFolder))
            {
                string subfolderName = Path.GetFileName(subfolder);
                string newRelativeSourcePath = Path.Combine(relativeSourcePath, subfolderName);
                string newSwrInstallDir = Path.Combine(swrInstallDir, subfolderName);

                // Don't propagate ngenAssemblies to subdirectories.
                AddFolder(sb, layoutDirectory, newRelativeSourcePath, newSwrInstallDir);
            }
        }

        private static readonly string SWR_HEADER = @"use vs

package name=Microsoft.Net.Core.SDK.RuntimeAnalyzers
        version=$(ProductsBuildVersion)
        vs.package.internalRevision=$(PackageInternalRevision)

";
    }
}
