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

            AddFolder(sb,
                      @"AspNetCoreAnalyzers",
                      @"SDK\AspNetCoreAnalyzers");

            AddFolder(sb,
                      @"NetCoreAnalyzers",
                      @"SDK\NetCoreAnalyzers");

            AddFolder(sb,
                      @"WindowsDesktopAnalyzers",
                      @"SDK\WindowsDesktopAnalyzers");

            AddFolder(sb,
                      @"SDKAnalyzers",
                      @"SDK\SDKAnalyzers");

            AddFolder(sb,
                      @"WebSDKAnalyzers",
                      @"SDK\WebSDKAnalyzers");

            File.WriteAllText(OutputFile, sb.ToString());

            return true;
        }

        private void AddFolder(StringBuilder sb, string relativeSourcePath, string swrInstallDir, bool ngenAssemblies = true)
        {
            string sourceFolder = Path.Combine(RuntimeAnalyzersLayoutDirectory, relativeSourcePath);
            var files = Directory.GetFiles(sourceFolder)
                            .Where(f => !Path.GetExtension(f).Equals(".pdb", StringComparison.OrdinalIgnoreCase) && !Path.GetExtension(f).Equals(".swr", StringComparison.OrdinalIgnoreCase))
                            .ToList();
            if (files.Any(f => !Path.GetFileName(f).Equals("_._")))
            {
                sb.Append(@"folder ""InstallDir:\");
                // remove the version number and everything until we get to the real analyzers folder
                Console.WriteLine(swrInstallDir);

                var startIndex = swrInstallDir.IndexOf("analyzers", StringComparison.OrdinalIgnoreCase);
                var endIndex = swrInstallDir.IndexOf("analyzers", startIndex + 1, StringComparison.OrdinalIgnoreCase);
                var tempinstalldir = swrInstallDir;
                if (startIndex >= 0 && endIndex >= 0)
                {
                    tempinstalldir = swrInstallDir.Remove(startIndex + 9, endIndex - startIndex );
                }
                sb.Append(tempinstalldir);
                sb.AppendLine(@"\""");

                foreach (var file in files)
                {
                    sb.Append(@"  file source=""$(PkgVS_Redist_Common_Net_Core_SDK_RuntimeAnalyzers)\");
                    sb.Append(Path.Combine(relativeSourcePath, Path.GetFileName(file)));
                    sb.Append('"');

                    if (ngenAssemblies && file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append(@" vs.file.ngenApplications=""[installDir]\Common7\IDE\vsn.exe""");
                    }

                    sb.AppendLine();
                }

                sb.AppendLine();
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

        readonly string SWR_HEADER = @"use vs

package name=Microsoft.Net.Core.SDK.RuntimeAnalyzers
        version=$(ProductsBuildVersion)
        vs.package.internalRevision=$(PackageInternalRevision)

";
    }
}
