using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests
{
    public class ArtifactsSize : SmokeTests
    {
        private readonly string baselineFilePath = BaselineHelper.GetBaselineFilePath("ArtifactsSize.txt");
        private readonly string[] baselineFileContent;

        public ArtifactsSize(ITestOutputHelper outputHelper) : base(outputHelper)
        {
            baselineFileContent = File.Exists(baselineFilePath) ? File.ReadAllLines(baselineFilePath) : Array.Empty<string>();
        }

        [SkippableFact(new[] { Config.SourceBuiltArtifactsPathEnv, Config.SdkTarballPathEnv }, skipOnNullOrWhiteSpace: true)]
        public void ArtifactsSizeTest()
        {
            Assert.NotNull(Config.SourceBuiltArtifactsPath);
            Assert.NotNull(Config.SdkTarballPath);

            IEnumerable<TarEntry> artifactsTarEntries = Utilities.GetTarballContent(Config.SourceBuiltArtifactsPath).Where(entry => entry.EntryType == TarEntryType.RegularFile);
            IEnumerable<TarEntry> sdkTarEntries = Utilities.GetTarballContent(Config.SdkTarballPath).Where(entry => entry.EntryType == TarEntryType.RegularFile);

            string[] tarEntries = sdkTarEntries.Concat(artifactsTarEntries)
                .Select(entry => $"{entry.Name}: {entry.Length} bytes")
                .OrderBy(entry => entry)
                .ToArray();

            foreach (string entry in tarEntries)
            {
                string tarEntryFilename = entry.Substring(0, entry.IndexOf(":")).Trim();
                string baselineEntry = baselineFileContent?.FirstOrDefault(baselineEntry => baselineEntry.StartsWith(tarEntryFilename)) ?? "";

                if (string.IsNullOrEmpty(baselineEntry))
                {
                    LogWarningMessage($"{tarEntryFilename} does not exist in baseline. Adding it to the baseline file.");
                    File.AppendAllText(baselineFilePath, $"{entry}" + Environment.NewLine);
                }
                else
                {
                    string tarEntrySize = entry.Substring(entry.IndexOf(":") + 1).Replace(" bytes", "").Trim();
                    string baselineEntrySize = baselineEntry.Substring(tarEntryFilename.Length + 1).Replace(" bytes", "").Trim() ?? "0";

                    string message = CompareFileSizes(tarEntryFilename, long.Parse(tarEntrySize), long.Parse(baselineEntrySize));
                    if (!string.IsNullOrEmpty(message))
                    {
                        LogWarningMessage(message);
                    }
                }
            }

            CopyBaselineFile();
        }

        private string CompareFileSizes(string filePath, long fileSize, long baselineSize)
        {
            if (fileSize == 0 && baselineSize != 0)
                return $"{filePath} is now 0 bytes. It was {baselineSize} bytes";
            else if (fileSize != 0 && baselineSize == 0)
                return $"{filePath} is no longer 0 bytes. It is now {fileSize} bytes";
            else if (baselineSize != 0 && Math.Abs(((fileSize - baselineSize) / (double)baselineSize) * 100) >= 25)
                return $"{filePath} increased in size by more than 25%. It was originally {baselineSize} bytes and is now {fileSize} bytes";
            return "";
        }

        private void LogWarningMessage(string message)
        {
            string prefix = "##vso[task.logissue type=warning;]";
            OutputHelper.WriteLine($"{Environment.NewLine}{prefix}{message}.{Environment.NewLine}");
            OutputHelper.WriteLine("##vso[task.complete result=SucceededWithIssues;]");
        }

        private void CopyBaselineFile()
        {
            try
            {
                string originalBaselineFilePath = baselineFilePath.Substring(0, baselineFilePath.IndexOf("/bin")) + "/assets/baselines/ArtifactsSize.txt";
                File.Copy(baselineFilePath, originalBaselineFilePath, true);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException($"An error occurred while copying the baselines file: {ex.Message}");
            }
        }
    }
}
