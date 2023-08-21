using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Formats.Tar;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

[Trait("Category", "SdkContent")]
public class ArtifactsSize : SmokeTests
{
    private readonly string baselineFilePath = BaselineHelper.GetBaselineFilePath($"ArtifactsSizes/{Config.TargetRid}.txt");
    private readonly string[] baselineFileContent;
    private readonly Regex buildVersionPattern = new(@"\b\d+\.\d+\.\d+[-@](alpha|preview|rc|rtm)\.\d(\.\d+\.\d+)?\b");


    public ArtifactsSize(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        baselineFileContent = File.Exists(baselineFilePath) ? File.ReadAllLines(baselineFilePath) : Array.Empty<string>();
    }

    [SkippableFact(new[] { Config.SourceBuiltArtifactsPathEnv, Config.SdkTarballPathEnv, Config.TargetRidEnv }, skipOnNullOrWhiteSpace: true)]
    public void ArtifactsSizeTest()
    {
        Assert.True(Directory.Exists(BaselineHelper.GetBaselineFilePath("ArtifactsSizes/")));
        Assert.NotNull(Config.SourceBuiltArtifactsPath);
        Assert.NotNull(Config.SdkTarballPath);
        Assert.NotNull(Config.TargetRid);

        IEnumerable<TarEntry> artifactsTarEntries = Utilities.GetTarballContent(Config.SourceBuiltArtifactsPath).Where(entry => entry.EntryType == TarEntryType.RegularFile);
        IEnumerable<TarEntry> sdkTarEntries = Utilities.GetTarballContent(Config.SdkTarballPath).Where(entry => entry.EntryType == TarEntryType.RegularFile);

        string[] tarEntries = sdkTarEntries.Concat(artifactsTarEntries)
            .Select(entry =>
            {
                string modifiedPath = buildVersionPattern.Replace(entry.Name, "VERSION");
                string result = modifiedPath.Replace(Config.TargetRid, "TARGET_RID");
                return $"{result}: {entry.Length} bytes";
            })
            .OrderBy(entry => entry)
            .ToArray();

        foreach (string entry in tarEntries)
        {
            string tarEntryFilename = entry.Substring(0, entry.IndexOf(":")).Trim();
            string baselineEntry = baselineFileContent?.FirstOrDefault(baselineEntry => baselineEntry.StartsWith(tarEntryFilename)) ?? "";

            if (string.IsNullOrEmpty(baselineEntry))
            {
                LogWarningMessage($"{tarEntryFilename} does not exist in baseline. Adding it to the baseline file");
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
            string originalBaselineFilePath = baselineFilePath.Substring(0, baselineFilePath.IndexOf("/bin")) + $"/assets/baselines/ArtifactsSizes/{Config.TargetRid}.txt";
            File.Copy(baselineFilePath, originalBaselineFilePath, true);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"An error occurred while copying the baselines file: {ex.Message}");
        }
    }
}
