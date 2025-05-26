// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.NET.Build.Containers.Tasks;

public class SelectRuntimeIdentifierSpecificItems : Microsoft.Build.Utilities.Task, ICancelableTask
{
    private CancellationTokenSource _cts = new();

    [Required]
    public string TargetRuntimeIdentifier { get; set; } = string.Empty;

    [Required]
    public ITaskItem[] Items { get; set; } = [];

    [Required]
    public string RuntimeIdentifierGraphPath { get; set; } = string.Empty;

    [Output]
    public ITaskItem[] SelectedItems { get; set; } = [];

    public void Cancel() => _cts.Cancel();

    public override bool Execute()
    {
        using MSBuildLoggerProvider loggerProvider = new(Log);
        ILoggerFactory msbuildLoggerFactory = new LoggerFactory(new[] { loggerProvider });
        ILogger logger = msbuildLoggerFactory.CreateLogger<CreateImageIndex>();
        var graph = NuGet.RuntimeModel.JsonRuntimeFormat.ReadRuntimeGraph(RuntimeIdentifierGraphPath);
        
        var selectedItems = new List<ITaskItem>(Items.Length);
        foreach (var item in Items)
        {
            if (item.GetMetadata("RuntimeIdentifier") is string ridValue && 
                graph.AreCompatible(TargetRuntimeIdentifier, ridValue))
            {
                selectedItems.Add(item);
            }
        }

        SelectedItems = selectedItems.ToArray();
        return true;
    }

    
}
