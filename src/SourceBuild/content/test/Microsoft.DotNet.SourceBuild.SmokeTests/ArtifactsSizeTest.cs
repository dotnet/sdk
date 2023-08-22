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
    private static readonly string BaselineFilePath = BaselineHelper.GetBaselineFilePath($"ArtifactsSizes/{Config.TargetRid}.txt");
    private static readonly Dictionary<string, long> BaselineFileContent = new Dictionary<string, long>();
    private static readonly Regex BuildVersionPattern = new(@"\b\d+\.\d+\.\d+[-@](alpha|preview|rc|rtm)\.\d(\.\d+\.\d+)?\b");


    public ArtifactsSize(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        if(File.Exists(BaselineFilePath))
        {
            string[] baselineFileContent = File.ReadAllLines(BaselineFilePath);
            foreach(string entry in baselineFileContent)
            {
                string[] splitEntry = entry.Split(':');
                BaselineFileContent[splitEntry[0].Trim()] = long.Parse(splitEntry[1].Trim());
            }
        }
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

        (string FilePath, long Bytes)[] tarEntries = sdkTarEntries.Concat(artifactsTarEntries)
            .Select(entry =>
            {
                string modifiedPath = buildVersionPattern.Replace(entry.Name, "VERSION");
                string result = BaselineHelper.RemoveRids(modifiedPath);
                return (result, entry.Length);
            })
            .OrderBy(entry => entry.FilePath)
            .ToArray();

        foreach (string entry in tarEntries)
        {
            if (BaselineFileContent.ContainsKey(entry.Filename))
            {
                LogWarningMessage($"{tarEntryFilename} does not exist in baseline. Adding it to the baseline file");
                File.AppendAllText(baselineFilePath, $"{entry}" + Environment.NewLine); // save writes to the end
            }
            else
            {
                CompareFileSizes(tarEntryFilename, long.Parse(tarEntrySize), long.Parse(baselineEntrySize));
            }
        }

        CopyBaselineFile();
    }

    private void CompareFileSizes(string filePath, long fileSize, long baselineSize)
    {
        if (fileSize == 0 && baselineSize != 0)
            LogWarningMessage($"'{filePath}' is now 0 bytes. It was {baselineSize} bytes");
        else if (fileSize != 0 && baselineSize == 0)
            LogWarningMessage($"'{filePath}' is no longer 0 bytes. It is now {fileSize} bytes");
        else if (baselineSize != 0 && Math.Abs(((fileSize - baselineSize) / (double)baselineSize) * 100) >= 25)
            LogWarningMessage($"'{filePath}' increased in size by more than 25%. It was originally {baselineSize} bytes and is now {fileSize} bytes");
        return;
    }

    // make an exdtension method in ITestOutputHelper
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
            string actualFilePath = Path.Combine(DotNetHelper.LogsDirectory, $"Updated_ArtifactsSizes_{Config.TargetRid}.txt");
            File.Copy(baselineFilePath, actualFilePath, true);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"An error occurred while copying the baselines file: {ex.Message}");
        }
    }
}
