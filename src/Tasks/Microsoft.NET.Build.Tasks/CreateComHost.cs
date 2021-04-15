// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.NET.HostModel;
using Microsoft.NET.HostModel.ComHost;

namespace Microsoft.NET.Build.Tasks
{
    public class CreateComHost : TaskWithAssemblyResolveHooks
    {
        private const int E_INVALIDARG = unchecked((int)0x80070057);

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
                if (!TryCreateTypeLibraryIdDictionary(TypeLibraries, out var typeLibIdMap))
                {
                    return;
                }
                ComHost.Create(
                    ComHostSourcePath,
                    ComHostDestinationPath,
                    ClsidMapPath,
                    typeLibIdMap);
            }
            catch (ComHostCustomizationUnsupportedOSException)
            {
                Log.LogError(Strings.CannotEmbedClsidMapIntoComhost);
            }
            catch (TypeLibraryDoesNotExistException ex)
            {
                Log.LogError(Strings.TypeLibraryDoesNotExist, ex.Path);
            }
            catch (InvalidTypeLibraryIdException ex)
            {
                Log.LogError(Strings.InvalidTypeLibraryId, ex.Id.ToString(), ex.Path);
            }
            catch (HResultException hr) when (hr.Win32HResult == E_INVALIDARG)
            {
                Log.LogError(Strings.InvalidTypeLibrary);
            }
        }

        private bool TryCreateTypeLibraryIdDictionary(ITaskItem[] typeLibraries, out Dictionary<int, string> typeLibraryIdMap)
        {
            typeLibraryIdMap = null;
            if (typeLibraries is null || typeLibraries.Length == 0)
            {
                return true;
            }
            else if (typeLibraries.Length == 1)
            {
                int id = 1;
                string idMetadata = typeLibraries[0].GetMetadata("Id");
                if (!string.IsNullOrEmpty(idMetadata))
                {
                    if (!int.TryParse(idMetadata, out id) || id == 0)
                    {
                        Log.LogError(Strings.InvalidTypeLibraryId, idMetadata, typeLibraries[0].ItemSpec);
                        return false;
                    }
                }
                typeLibraryIdMap = new Dictionary<int, string> { { id, typeLibraries[0].ItemSpec } };
                return true;
            }
            typeLibraryIdMap = new Dictionary<int, string>();
            foreach (ITaskItem typeLibrary in typeLibraries)
            {
                string idMetadata = typeLibrary.GetMetadata("Id");
                if (string.IsNullOrEmpty(idMetadata))
                {
                    Log.LogError(Strings.MissingTypeLibraryId, typeLibrary.ItemSpec);
                    return false;
                }

                if (!int.TryParse(idMetadata, out int id) || id == 0)
                {
                    Log.LogError(Strings.InvalidTypeLibraryId, idMetadata, typeLibrary.ItemSpec);
                    return false;
                }

                if (typeLibraryIdMap.ContainsKey(id))
                {
                    Log.LogError(Strings.DuplicateTypeLibraryIds, idMetadata, typeLibraryIdMap[id], typeLibrary.ItemSpec);
                    return false;
                }
                else
                {
                    typeLibraryIdMap[id] = typeLibrary.ItemSpec;
                }
            }
            return true;
        }
    }
}
