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
        /// <summary>
        /// Gets or sets the task environment for thread-safe operations.
        /// </summary>
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

                string comHostSourcePath = TaskEnvironment?.GetAbsolutePath(ComHostSourcePath) ?? ComHostSourcePath;
                string comHostDestinationPath = TaskEnvironment?.GetAbsolutePath(ComHostDestinationPath) ?? ComHostDestinationPath;
                string clsidMapPath = TaskEnvironment?.GetAbsolutePath(ClsidMapPath) ?? ClsidMapPath;

                ComHost.Create(
                    comHostSourcePath,
                    comHostDestinationPath,
                    clsidMapPath,
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
