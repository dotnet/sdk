// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    static class TypeLibraryDictionaryBuilder
    {
        public static bool TryCreateTypeLibraryIdDictionary(ITaskItem[] typeLibraries, out Dictionary<int, string> typeLibraryIdMap, out List<string> errors)
        {
            typeLibraryIdMap = null;
            errors = new List<string>();
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
                        errors.Add(string.Format(Strings.InvalidTypeLibraryId, idMetadata, typeLibraries[0].ItemSpec));
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
                    errors.Add(string.Format(Strings.MissingTypeLibraryId, typeLibrary.ItemSpec));
                    continue;
                }

                if (!int.TryParse(idMetadata, out int id) || id == 0)
                {
                    errors.Add(string.Format(Strings.InvalidTypeLibraryId, idMetadata, typeLibrary.ItemSpec));
                    continue;
                }

                if (typeLibraryIdMap.ContainsKey(id))
                {
                    errors.Add(string.Format(Strings.DuplicateTypeLibraryIds, idMetadata, typeLibraryIdMap[id], typeLibrary.ItemSpec));
                }
                else
                {
                    typeLibraryIdMap[id] = typeLibrary.ItemSpec;
                }
            }
            return errors.Count == 0;
        }
    }
}
