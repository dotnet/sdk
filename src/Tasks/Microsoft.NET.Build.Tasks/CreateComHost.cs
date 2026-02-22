// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.NET.HostModel.ComHost;

namespace Microsoft.NET.Build.Tasks
{
    [MSBuildMultiThreadableTask]
    public class CreateComHost : TaskBase, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        [Required]
        public string ComHostSourcePath { get; set; }

        [Required]
        public string ComHostDestinationPath { get; set; }

        [Required]
        public string ClsidMapPath { get; set; }

        public ITaskItem[] TypeLibraries { get; set; }

        protected override void ExecuteCore()
        {
            try
            {
                if (!TypeLibraryDictionaryBuilder.TryCreateTypeLibraryIdDictionary(
                    TypeLibraries,
                    out Dictionary<int, string> typeLibIdMap,
                    out IEnumerable<string> errors))
                {
                    foreach (string error in errors)
                    {
                        Log.LogError(error);
                    }
                    return;
                }

                // Absolutize type library paths
                if (typeLibIdMap != null)
                {
                    foreach (var key in typeLibIdMap.Keys.ToList())
                    {
                        typeLibIdMap[key] = TaskEnvironment.GetAbsolutePath(typeLibIdMap[key]);
                    }
                }

                ComHost.Create(
                    TaskEnvironment.GetAbsolutePath(ComHostSourcePath),
                    TaskEnvironment.GetAbsolutePath(ComHostDestinationPath),
                    TaskEnvironment.GetAbsolutePath(ClsidMapPath),
                    typeLibIdMap);
            }
            catch (TypeLibraryDoesNotExistException ex)
            {
                Log.LogError(Strings.TypeLibraryDoesNotExist, ex.Path);
            }
            catch (InvalidTypeLibraryIdException ex)
            {
                Log.LogError(Strings.InvalidTypeLibraryId, ex.Id.ToString(), ex.Path);
            }
            catch (InvalidTypeLibraryException ex)
            {
                Log.LogError(Strings.InvalidTypeLibrary, ex.Path);
            }
        }
    }
}
