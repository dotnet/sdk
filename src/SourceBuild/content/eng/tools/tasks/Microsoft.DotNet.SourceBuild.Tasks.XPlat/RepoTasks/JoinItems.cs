// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// Copied from https://github.com/aspnet/Universe/blob/1f8f30a1e834eff147ced0c669cef8828f9511c8/build/tasks/JoinItems.cs.
// When this task is available in https://github.com/dotnet/Arcade, switch to use that version.
// Modified to allow multiple Right matches using GroupJoin.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Linq;

namespace RepoTasks
{
    public class JoinItems : Task
    {
        [Required]
        public ITaskItem[] Left { get; set; }

        [Required]
        public ITaskItem[] Right { get; set; }

        // The metadata to use as the new item spec. If not specified, LeftKey is used.
        public string LeftItemSpec { get; set; }

        //  LeftKey and RightKey: The metadata to join on.  If not set, then use the ItemSpec
        public string LeftKey { get; set; }

        public string RightKey { get; set; }


        //  LeftMetadata and RightMetadata: The metadata names to include in the result.  Specify "*" to include all metadata
        public string[] LeftMetadata { get; set; }

        public string[] RightMetadata { get; set; }


        [Output]
        public ITaskItem[] JoinResult { get; private set; }

        public override bool Execute()
        {
            bool useAllLeftMetadata = LeftMetadata != null && LeftMetadata.Length == 1 && LeftMetadata[0] == "*";
            bool useAllRightMetadata = RightMetadata != null && RightMetadata.Length == 1 && RightMetadata[0] == "*";
            var newItemSpec = string.IsNullOrEmpty(LeftItemSpec)
                ? LeftKey
                : LeftItemSpec;

            JoinResult = Left.GroupJoin(Right,
                item => GetKeyValue(LeftKey, item),
                item => GetKeyValue(RightKey, item),
                (left, rights) =>
                {
                    //  If including all metadata from left items and none from right items, just return left items directly
                    if (useAllLeftMetadata &&
                        string.IsNullOrEmpty(LeftKey) &&
                        string.IsNullOrEmpty(LeftItemSpec) &&
                        (RightMetadata == null || RightMetadata.Length == 0))
                    {
                        return left;
                    }

                    //  If including all metadata from all right items and none from left items, just return the right items directly
                    if (useAllRightMetadata &&
                        string.IsNullOrEmpty(RightKey) &&
                        string.IsNullOrEmpty(LeftItemSpec) &&
                        (LeftMetadata == null || LeftMetadata.Length == 0))
                    {
                        return rights.Aggregate(
                            new TaskItem(),
                            (agg, next) =>
                            {
                                CopyAllMetadata(next, agg);
                                return agg;
                            });
                    }

                    var ret = new TaskItem(GetKeyValue(newItemSpec, left));

                    //  Weird ordering here is to prefer left metadata in all cases, as CopyToMetadata doesn't overwrite any existing metadata
                    if (useAllLeftMetadata)
                    {
                        CopyAllMetadata(left, ret);
                    }

                    if (!useAllRightMetadata && RightMetadata != null)
                    {
                        foreach (string name in RightMetadata)
                        {
                            foreach (var right in rights)
                            {
                                ret.SetMetadata(name, right.GetMetadata(name));
                            }
                        }
                    }

                    if (!useAllLeftMetadata && LeftMetadata != null)
                    {
                        foreach (string name in LeftMetadata)
                        {
                            ret.SetMetadata(name, left.GetMetadata(name));
                        }
                    }

                    if (useAllRightMetadata)
                    {
                        foreach (var right in rights)
                        {
                            CopyAllMetadata(right, ret);
                        }
                    }

                    return (ITaskItem)ret;
                },
                StringComparer.OrdinalIgnoreCase).ToArray();

            return true;
        }

        static void CopyAllMetadata(ITaskItem source, ITaskItem dest)
        {
            //  CopyMetadata adds an OriginalItemSpec, which we don't want.  So we subsequently remove it
            source.CopyMetadataTo(dest);
            dest.RemoveMetadata("OriginalItemSpec");
        }

        static string GetKeyValue(string key, ITaskItem item)
        {
            if (string.IsNullOrEmpty(key))
            {
                return item.ItemSpec;
            }
            else
            {
                return item.GetMetadata(key);
            }
        }
    }
}
