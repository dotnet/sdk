// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.Tasks;

[Flags]
internal enum ContainerAnnotationScope
{
    Manifest = 1,
    Index = 2,
}

internal static class ContainerAnnotationScopes
{
    internal static bool TryFilter(ITaskItem[] annotations, ContainerAnnotationScope requestedScope, TaskLoggingHelper log, out ITaskItem[] filtered)
    {
        List<ITaskItem> result = new(annotations.Length);
        foreach (ITaskItem annotation in annotations)
        {
            string scopeMetadata = annotation.GetMetadata("Scope");
            ContainerAnnotationScope scope = requestedScope;

            if (!string.IsNullOrWhiteSpace(scopeMetadata))
            {
                scope = 0;
                foreach (string value in scopeMetadata.Split(','))
                {
                    if (value.Trim().Equals(nameof(ContainerAnnotationScope.Manifest), StringComparison.OrdinalIgnoreCase))
                    {
                        scope |= ContainerAnnotationScope.Manifest;
                    }
                    else if (value.Trim().Equals(nameof(ContainerAnnotationScope.Index), StringComparison.OrdinalIgnoreCase))
                    {
                        scope |= ContainerAnnotationScope.Index;
                    }
                    else
                    {
                        log.LogError(string.Format(Resource.GetString("InvalidContainerAnnotationScope"), annotation.ItemSpec, scopeMetadata));
                        filtered = Array.Empty<ITaskItem>();
                        return false;
                    }
                }
            }

            if ((scope & requestedScope) != 0)
            {
                result.Add(annotation);
            }
        }

        filtered = result.ToArray();
        return true;
    }
}
