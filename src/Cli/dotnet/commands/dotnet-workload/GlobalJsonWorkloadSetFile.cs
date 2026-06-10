// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload
{
    internal class GlobalJsonWorkloadSetsFile
    {
        string _path;

        public string Path { get { return _path; } }

        private static JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public GlobalJsonWorkloadSetsFile(SdkFeatureBand sdkFeatureBand, string dotnetDir)
        {
            _path = System.IO.Path.Combine(WorkloadInstallType.GetInstallStateFolder(sdkFeatureBand, dotnetDir), "globaljsonworkloadsets.json");
        }

        public void RecordWorkloadSetInGlobalJson(string globalJsonPath, string workloadSetVersion)
        {
            //  Create install state folder if needed
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path));

            using (var fileStream = File.Open(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                Dictionary<string, string> globalJsonWorkloadSetVersions;
                if (fileStream.Length > 0)
                {
                    globalJsonWorkloadSetVersions = JsonSerializer.Deserialize<Dictionary<string, string>>(fileStream, _jsonSerializerOptions);
                }
                else
                {
                    globalJsonWorkloadSetVersions = new();
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
                    return File.Open(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
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
                    return new Dictionary<string, string>();
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

        string GetWorkloadVersionFromGlobalJson(string globalJsonPath)
        {
            try
            {
                return SdkDirectoryWorkloadManifestProvider.GlobalJsonReader.GetWorkloadVersionFromGlobalJson(globalJsonPath);
            }
            catch (SdkDirectoryWorkloadManifestProvider.JsonFormatException)
            {
                //  If Json file is malformed then treat it as if it doesn't specify a workload set version
                return null;
            }
        }
    }
}
