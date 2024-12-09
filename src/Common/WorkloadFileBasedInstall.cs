// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable IDE0240
#nullable enable
#pragma warning restore IDE0240

using System.Text.Json;
#if DotnetCsproj
using Microsoft.DotNet.Workloads.Workload.History;
#endif
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload
{
    static class WorkloadFileBasedInstall
    {
        public static bool IsUserLocal(string? dotnetDir, string? sdkFeatureBand)
            => dotnetDir is not null && File.Exists(GetUserInstallFilePath(dotnetDir, sdkFeatureBand));

        internal static void SetUserLocal(string dotnetDir, string sdkFeatureBand)
        {
            string filePath = GetUserInstallFilePath(dotnetDir, sdkFeatureBand);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "");
        }

        private static string GetUserInstallFilePath(string dotnetDir, string? sdkFeatureBand)
        {
            if (sdkFeatureBand is not null && (sdkFeatureBand.Contains("-") || !sdkFeatureBand.EndsWith("00", StringComparison.Ordinal)))
            {
                // The user passed in the sdk version. Derive the feature band version.
                if (!Version.TryParse(sdkFeatureBand.Split('-')[0], out var sdkVersionParsed))
                {
                    throw new FormatException($"'{nameof(sdkFeatureBand)}' should be a version, but get {sdkFeatureBand}");
                }

                static int Last2DigitsTo0(int versionBuild)
                {
                    return (versionBuild / 100) * 100;
                }

                sdkFeatureBand = $"{sdkVersionParsed.Major}.{sdkVersionParsed.Minor}.{Last2DigitsTo0(sdkVersionParsed.Build)}";
            }

            return Path.Combine(dotnetDir, "metadata", "workloads", new SdkFeatureBand(sdkFeatureBand).ToString(), "userlocal");
        }

#if DotnetCsproj
        public static IEnumerable<WorkloadHistoryRecord> GetWorkloadHistoryRecords(string workloadHistoryDirectory)
        {
            if (!Directory.Exists(workloadHistoryDirectory))
            {
                return Enumerable.Empty<WorkloadHistoryRecord>();
            }

            List<WorkloadHistoryRecord> historyRecords = new();

            foreach (var file in Directory.GetFiles(workloadHistoryDirectory, "*.json"))
            {
                try
                {
                    var historyRecord = JsonSerializer.Deserialize<WorkloadHistoryRecord>(File.ReadAllText(file));
                    if (historyRecord is not null)
                    {
                        historyRecords.Add(historyRecord);
                    }
                }
                catch (JsonException)
                {
                    // We picked up a file that wasn't in the correct format, but this isn't necessarily a problem, since we take all json files from
                    // the workload history directory. Just ignore it.
                }
            }

            return historyRecords;
        }
#endif
    }
}
