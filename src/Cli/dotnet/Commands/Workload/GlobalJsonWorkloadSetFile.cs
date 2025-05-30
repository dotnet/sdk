﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal class GlobalJsonWorkloadSetsFile(SdkFeatureBand sdkFeatureBand, string dotnetDir)
{
    public string Path { get; } = System.IO.Path.Combine(WorkloadInstallType.GetInstallStateFolder(sdkFeatureBand, dotnetDir), "globaljsonworkloadsets.json");

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public void RecordWorkloadSetInGlobalJson(string globalJsonPath, string workloadSetVersion)
    {
        //  Create install state folder if needed
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));

        using (var fileStream = File.Open(Path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            Dictionary<string, string> globalJsonWorkloadSetVersions;
            if (fileStream.Length > 0)
            {
                globalJsonWorkloadSetVersions = JsonSerializer.Deserialize<Dictionary<string, string>>(fileStream, _jsonSerializerOptions);
            }
            else
            {
                globalJsonWorkloadSetVersions = [];
            }
            globalJsonWorkloadSetVersions[globalJsonPath] = workloadSetVersion;
            fileStream.Seek(0, SeekOrigin.Begin);
            JsonSerializer.Serialize(fileStream, globalJsonWorkloadSetVersions, _jsonSerializerOptions);
        }
    }

    public Dictionary<string, string> GetGlobalJsonWorkloadSetVersions()
    {
        //  If the file doesn't exist, we don't need to create it
        FileStream OpenFile()
        {
            try
            {
                return File.Open(Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }

        using (var fileStream = OpenFile())
        {
            if (fileStream == null)
            {
                return [];
            }

            var globalJsonWorkloadSetVersions = JsonSerializer.Deserialize<Dictionary<string, string>>(fileStream, _jsonSerializerOptions);
            bool updated = false;

            //  Create copy of dictionary for iteration so we can modify the original in the loop
            foreach (var kvp in new Dictionary<string, string>(globalJsonWorkloadSetVersions))
            {
                var updatedVersion = GetWorkloadVersionFromGlobalJson(kvp.Key);
                //  If the version in global.json has changed, we remove it from the GC root file rather than updating it.
                //  This avoids having workload sets in the GC Root file which may not be installed, which makes
                //  garbage collection easier
                if (updatedVersion == null || updatedVersion != kvp.Value)
                {
                    updated = true;
                    globalJsonWorkloadSetVersions.Remove(kvp.Key);
                }
            }

            if (updated)
            {
                fileStream.Seek(0, SeekOrigin.Begin);
                JsonSerializer.Serialize(fileStream, globalJsonWorkloadSetVersions, _jsonSerializerOptions);
            }

            return globalJsonWorkloadSetVersions;
        }
    }

    private static string GetWorkloadVersionFromGlobalJson(string globalJsonPath)
    {
        try
        {
            return SdkDirectoryWorkloadManifestProvider.GlobalJsonReader.GetWorkloadVersionFromGlobalJson(globalJsonPath, out _);
        }
        catch (SdkDirectoryWorkloadManifestProvider.JsonFormatException)
        {
            //  If Json file is malformed then treat it as if it doesn't specify a workload set version
            return null;
        }
    }
}
