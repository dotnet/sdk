﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Configurer;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.NET.Build.Tasks
{
    public class ShowMissingWorkloads : TaskBase
    {
        private static readonly string MauiCrossPlatTopLevelVSWorkloads = "Microsoft.VisualStudio.Workload.NetCrossPlat";
        private static readonly string MauiComponentGroupVSWorkload = "Microsoft.VisualStudio.ComponentGroup.Maui.All";
        private static readonly string WasmTopLevelVSWorkload = "Microsoft.VisualStudio.Workload.NetWeb";
        private static readonly HashSet<string> MauiWorkloadIds = new(StringComparer.OrdinalIgnoreCase)
            { "android", "android-aot", "ios", "maccatalyst", "macos", "maui", "maui-android",
            "maui-desktop", "maui-ios", "maui-maccatalyst", "maui-mobile", "maui-windows", "tvos" };
        private static readonly HashSet<string> WasmWorkloadIds = new(StringComparer.OrdinalIgnoreCase)
            { "wasm-tools", "wasm-tools-net6", "wasm-tools-net7" };

        public ITaskItem[] MissingWorkloadPacks { get; set; }

        public string NetCoreRoot { get; set; }

        public string NETCoreSdkVersion { get; set; }

        public bool GenerateErrorsForMissingWorkloads { get; set; }

        [Output]
        public ITaskItem[] SuggestedWorkloads { get; set; }

        protected override void ExecuteCore()
        {
            if (MissingWorkloadPacks.Any())
            {
                string? userProfileDir = CliFolderPathCalculatorCore.GetDotnetUserProfileFolderPath();

                //  When running MSBuild tasks, the current directory is always the project directory, so we can use that as the
                //  starting point to search for global.json
                string globalJsonPath = SdkDirectoryWorkloadManifestProvider.GetGlobalJsonPath(Environment.CurrentDirectory);

                var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(NetCoreRoot, NETCoreSdkVersion, userProfileDir, globalJsonPath);
                var workloadResolver = Create(workloadManifestProvider, NetCoreRoot, NETCoreSdkVersion, userProfileDir);

                var suggestedWorkloads = workloadResolver.GetWorkloadSuggestionForMissingPacks(
                    MissingWorkloadPacks.Select(item => new WorkloadPackId(item.ItemSpec)).ToList(),
                    out ISet<WorkloadPackId> unsatisfiablePacks
                );

                if (GenerateErrorsForMissingWorkloads)
                {
                    if (suggestedWorkloads is not null)
                    {
                        var errorMessage = string.Format(CultureInfo.CurrentCulture,
                            Strings.WorkloadNotInstalled, string.Join(" ", suggestedWorkloads.Select(w => w.Id)));
                        Log.LogError(errorMessage);
                    }
                    else
                    {
                        Log.LogError(Strings.WorkloadNotAvailable, string.Join(" ", unsatisfiablePacks));
                    }
                }

                if (suggestedWorkloads is not null)
                {
                    SuggestedWorkloads = suggestedWorkloads.Select(suggestedWorkload =>
                    {
                        var suggestedWorkloadsList = GetSuggestedWorkloadsList(suggestedWorkload);
                        var taskItem = new TaskItem(suggestedWorkload.Id);
                        taskItem.SetMetadata("VisualStudioComponentId", ToSafeId(suggestedWorkload.Id));
                        taskItem.SetMetadata("VisualStudioComponentIds", string.Join(";", suggestedWorkloadsList));
                        return taskItem;
                    }).ToArray();
                }
            }
        }

        internal static string ToSafeId(string id)
        {
            return id.Replace("-", ".").Replace(" ", ".").Replace("_", ".");
        }

        private static IEnumerable<string> GetSuggestedWorkloadsList(WorkloadInfo workloadInfo)
        {
            yield return ToSafeId(workloadInfo.Id);
            if (MauiWorkloadIds.Contains(workloadInfo.Id.ToString()))
            {
                yield return MauiCrossPlatTopLevelVSWorkloads;
                yield return MauiComponentGroupVSWorkload;
            }
            if (WasmWorkloadIds.Contains(workloadInfo.Id.ToString()))
            {
                yield return WasmTopLevelVSWorkload;
            }
        }
    }
}
