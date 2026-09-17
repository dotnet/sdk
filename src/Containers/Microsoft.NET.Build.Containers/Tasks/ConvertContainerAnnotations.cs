// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Containers.Tasks;

public sealed class ConvertContainerAnnotations : Microsoft.Build.Utilities.Task
{
    public ITaskItem[] Annotations { get; set; } = Array.Empty<ITaskItem>();

    public string SerializedAnnotations { get; set; } = string.Empty;

    [Output]
    public string EncodedAnnotations { get; set; } = string.Empty;

    [Output]
    public ITaskItem[] DecodedAnnotations { get; set; } = Array.Empty<ITaskItem>();

    public override bool Execute()
    {
        try
        {
            if (!string.IsNullOrEmpty(SerializedAnnotations))
            {
                AnnotationData[] data = JsonSerializer.Deserialize<AnnotationData[]>(Convert.FromBase64String(SerializedAnnotations)) ?? Array.Empty<AnnotationData>();
                DecodedAnnotations = data.Select(annotation =>
                {
                    TaskItem item = new(annotation.Identity);
                    item.SetMetadata("Scope", annotation.Scope);
                    item.SetMetadata("Value", annotation.Value);
                    return (ITaskItem)item;
                }).ToArray();
            }
            else
            {
                AnnotationData[] data = Annotations.Select(annotation => new AnnotationData(
                    annotation.ItemSpec,
                    annotation.GetMetadata("Scope"),
                    annotation.GetMetadata("Value"))).ToArray();
                EncodedAnnotations = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(data));
            }
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            Log.LogErrorFromException(exception, showStackTrace: false);
        }

        return !Log.HasLoggedErrors;
    }

    private sealed record AnnotationData(string Identity, string Scope, string Value);
}
