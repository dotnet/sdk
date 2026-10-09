// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text;

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class Crossgen : ToolTask
    {
        [Required]
        public string SourceAssembly { get; set; }

        [Required]
        public string DestinationPath { get; set; }

        [Required]
        public string TargetArchitecture { get; set; }

        [Required]
        public string TargetOS { get; set; }

        public string CrossgenPath { get; set; }

        public bool CreateSymbols { get; set; }

        public ITaskItem[] CrossModuleInliningAssemblies { get; set; }

        public string NonLocalGenericsModule { get; set; }

        public ITaskItem[] PlatformAssemblyPaths { get; set; }

        private string TempOutputPath { get; set; }

        protected override bool ValidateParameters()
        {
            if (!base.ValidateParameters())
            {
                return false;
            }

            if (!File.Exists(SourceAssembly))
            {
                Log.LogError($"Source assembly '{SourceAssembly}' does not exist.");
                return false;
            }

            return true;
        }

        public override bool Execute()
        {
            string tempDirPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirPath);
            TempOutputPath = Path.Combine(tempDirPath, Path.GetFileName(DestinationPath));

            try
            {
                bool toolResult = base.Execute();
                if (!toolResult)
                {
                    return false;
                }

                if (!File.Exists(TempOutputPath))
                {
                    Log.LogError($"Crossgen2 did not produce output assembly '{TempOutputPath}'.");
                    return false;
                }

                string destinationDirectory = Path.GetDirectoryName(DestinationPath);
                foreach (string file in Directory.GetFiles(tempDirPath))
                {
                    File.Copy(file, Path.Combine(destinationDirectory, Path.GetFileName(file)), overwrite: true);
                }

                return true;
            }
            finally
            {
                if (Directory.Exists(tempDirPath))
                {
                    Directory.Delete(tempDirPath, recursive: true);
                }
            }
        }

        protected override string ToolName => "crossgen2";

        // Default is low, but we want to see output at normal verbosity.
        protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.Normal;

        // This turns stderr messages into msbuild errors below.
        protected override MessageImportance StandardErrorLoggingImportance => MessageImportance.High;

        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            // Crossgen's error/warning formatting is inconsistent and so we do
            // not use the "canonical error format" handling of base.
            //
            // Furthermore, we don't want to log crossgen warnings as msbuild
            // warnings because we cannot prevent them and they are only
            // occasionally formatted as something that base would recognize as
            // a canonically formatted warning anyway.
            //
            // One thing that is consistent is that crossgen errors go to stderr
            // and everything else goes to stdout. Above, we set stderr to high
            // importance above, and stdout to normal. So we can use that here
            // to distinguish between errors and messages.
            if (messageImportance == MessageImportance.High)
            {
                Log.LogError(singleLine);
            }
            else
            {
                Log.LogMessage(messageImportance, singleLine);
            }
        }

        protected override string GenerateFullPathToTool() => CrossgenPath ?? "crossgen2";

        protected override string GenerateCommandLineCommands() => GenerateCommandLineCommands(TempOutputPath);

        private string GenerateCommandLineCommands(string outputPath)
            => $"{GetInPath()} -o \"{outputPath}\" {GetTargetOS()} {GetArchitecture()} {GetPlatformAssemblyPaths()} {GetCreateSymbols()} {GetCrossModuleOptions()}".Trim();

        private string GetArchitecture() => $"--targetarch {TargetArchitecture}";

        private string GetTargetOS() => $"--targetos {TargetOS}";

        private string GetCreateSymbols()
            => CreateSymbols
                ? RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "--pdb" : "--perfmap"
                : string.Empty;

        private string GetInPath() => $"\"{SourceAssembly}\"";

        private string GetPlatformAssemblyPaths()
        {
            var platformAssemblyPaths = new StringBuilder();
            if (PlatformAssemblyPaths != null)
            {
                foreach (var platformAssemblyPath in PlatformAssemblyPaths)
                {
                    platformAssemblyPaths.Append($"-r \"{platformAssemblyPath.ItemSpec}{Path.DirectorySeparatorChar}*.dll\" ");
                }
            }

            return platformAssemblyPaths.ToString();
        }

        private string GetCrossModuleOptions()
        {
            var options = new StringBuilder();
            if (CrossModuleInliningAssemblies != null)
            {
                foreach (var assembly in CrossModuleInliningAssemblies)
                {
                    options.Append($"--opt-cross-module:\"{assembly.ItemSpec}\" ");
                }
            }

            if (!string.IsNullOrEmpty(NonLocalGenericsModule))
            {
                options.Append($"--non-local-generics-module:\"{NonLocalGenericsModule}\"");
            }

            return options.ToString();
        }

        protected override void LogToolCommand(string message) => base.LogToolCommand($"{GetWorkingDirectory()}> {message}");
    }
}
