// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.Serialization.Json;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Watch;
using Task = Microsoft.Build.Utilities.Task;

namespace DotNetWatchTasks
{
    public class FileSetSerializer : Task
    {
        [Required]
        public ITaskItem[] WatchFiles { get; set; } = null!;

        [Required]
        public string OutputPath { get; set; } = null!;

        public override bool Execute()
        {
            var projectItems = new Dictionary<string, ProjectItems>();
            var fileSetResult = new MSBuildFileSetResult
            {
                Projects = projectItems
            };

            foreach (var item in WatchFiles)
            {
                var fullPath = item.GetMetadata("FullPath");
                var staticWebAssetPath = item.GetMetadata("StaticWebAssetPath");

                // containing project path:
                var projectFullPath = item.GetMetadata("ProjectFullPath");

                if (!projectItems.TryGetValue(projectFullPath, out var project))
                {
                    projectItems.Add(projectFullPath, project = new ProjectItems());
                }

                // The following eliminates duplicate entries originating from diamond dependencies.
                // Each project evaluates _CollectWatchItems target once and the result is reused.
                // The resulting items are then merged into items of the referencing project.
                // If a single project is referenced from two different projects their common ancestor
                // will see multiple entries for the files.

                // Note: We don't track files per TFM.

                if (string.IsNullOrEmpty(staticWebAssetPath))
                {
                    project.FileSetBuilder.Add(fullPath);
                }
                else if (!project.StaticFileSetBuilder.TryGetValue(fullPath, out var existingStaticWebAssetPath))
                {
                    project.StaticFileSetBuilder.Add(fullPath, staticWebAssetPath);
                }
                else
                {
                    Debug.Assert(existingStaticWebAssetPath == staticWebAssetPath);
                }
            }

            foreach (var projectItem in projectItems)
            {
                projectItem.Value.PrepareForSerialization();
            }

            var serializer = new DataContractJsonSerializer(fileSetResult.GetType(), new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true,
            });

            using var fileStream = File.Create(OutputPath);
            using var writer = JsonReaderWriterFactory.CreateJsonWriter(fileStream, Encoding.UTF8, ownsStream: false, indent: true);
            serializer.WriteObject(writer, fileSetResult);

            return !Log.HasLoggedErrors;
        }
    }
}
