// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli.Build
{
    public class GenerateRuntimeAnalyzersSWR : Task
    {
        [Required]
        public string RuntimeAnalyzersLayoutDirectory { get; set; }

        [Required]
        public string OutputFile { get; set; }

        public override bool Execute()
        {
            StringBuilder sb = new StringBuilder(SWR_HEADER);

            // NOTE: Keep in sync with SdkAnalyzerAssemblyRedirector.
            // This is intentionally short to avoid long path problems.
            const string installDir = @"DotNetRuntimeAnalyzers";

            AddFolder(sb,
                      @"AnalyzerRedirecting",
                      @"Common7\IDE\CommonExtensions\Microsoft\AnalyzerRedirecting",
                      filesToInclude:
                      [
                          "Microsoft.Net.Sdk.AnalyzerRedirecting.dll",
                          "Microsoft.Net.Sdk.AnalyzerRedirecting.pkgdef",
                          "extension.vsixmanifest",
                      ]);

            AddFolder(sb,
                      @"AspNetCoreAnalyzers",
                      @$"{installDir}\AspNetCoreAnalyzers");

            AddFolder(sb,
                      @"NetCoreAnalyzers",
                      @$"{installDir}\NetCoreAnalyzers");

            AddFolder(sb,
                      @"WindowsDesktopAnalyzers",
                      @$"{installDir}\WindowsDesktopAnalyzers");

            AddFolder(sb,
                      @"SDKAnalyzers",
                      @$"{installDir}\SDKAnalyzers");

            AddFolder(sb,
                      @"WebSDKAnalyzers",
                      @$"{installDir}\WebSDKAnalyzers");

            File.WriteAllText(OutputFile, sb.ToString());

            return true;
        }

        private void AddFolder(StringBuilder sb, string relativeSourcePath, string swrInstallDir, bool ngenAssemblies = true, IEnumerable<string> filesToInclude = null)
        {
            string sourceFolder = Path.Combine(RuntimeAnalyzersLayoutDirectory, relativeSourcePath);

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
                AddFolder(sb, newRelativeSourcePath, newSwrInstallDir);
            }
        }

        private static readonly string SWR_HEADER = @"use vs

package name=Microsoft.Net.Core.SDK.RuntimeAnalyzers
        version=$(ProductsBuildVersion)
        vs.package.internalRevision=$(PackageInternalRevision)

";
    }
}
