// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Containers.Tasks;

internal sealed class VSHostObject(ITaskHost? hostObject, TaskLoggingHelper log)
{
    private const string CredentialItemSpecName = "MsDeployCredential";
    private const string UserMetaDataName = "UserName";
    private const string PasswordMetaDataName = "Password";

    private readonly ITaskHost? _hostObject = hostObject;
    private readonly TaskLoggingHelper _log = log;

    /// <summary>
    /// Tries to extract credentials from the host object.
    /// </summary>
    /// <returns>A tuple of (username, password) if credentials were found with non-empty username, null otherwise.</returns>
    public (string username, string password)? TryGetCredentials()
    {
        if (_hostObject is null)
        {
            return null;
        }

        IEnumerable<ITaskItem>? taskItems = GetTaskItems();
        if (taskItems is null)
        {
            _log.LogMessage(MessageImportance.Low, "No task items found in host object.");
            return null;
        }

        ITaskItem? credentialItem = taskItems.FirstOrDefault(p => p.ItemSpec == CredentialItemSpecName);
        if (credentialItem is null)
        {
            return null;
        }

        string username = credentialItem.GetMetadata(UserMetaDataName);
        if (string.IsNullOrEmpty(username))
        {
            return null;
        }

        string password = credentialItem.GetMetadata(PasswordMetaDataName);
        return (username, password);
    }

    private IEnumerable<ITaskItem>? GetTaskItems()
    {
        try
        {
            // This call mirrors the behavior of Microsoft.WebTools.Publish.MSDeploy.VSMsDeployTaskHostObject.QueryAllTaskItems.
            // Expected contract:
            //   - Instance method on the host object named "QueryAllTaskItems".
            //   - Signature: string QueryAllTaskItems().
            //   - Returns a JSON array of objects with the shape:
            //       [{ "ItemSpec": "<string>", "Metadata": { "<key>": "<value>", ... } }, ...]
            // The JSON is deserialized into TaskItemDto records and converted to ITaskItem instances.
            // Only UserName and Password metadata are extracted to avoid conflicts with reserved MSBuild metadata.
            string? rawTaskItems = (string?)_hostObject!.GetType().InvokeMember(
                "QueryAllTaskItems",
                BindingFlags.InvokeMethod,
                null,
                _hostObject,
                null);

            if (!string.IsNullOrEmpty(rawTaskItems))
            {
                List<TaskItemDto>? dtos = JsonSerializer.Deserialize<List<TaskItemDto>>(rawTaskItems);
                if (dtos is not null && dtos.Count > 0)
                {
                    _log.LogMessage(MessageImportance.Low, "Successfully retrieved task items via QueryAllTaskItems.");
                    return dtos.Select(ConvertToTaskItem).ToList();
                }
            }

            _log.LogMessage(MessageImportance.Low, "QueryAllTaskItems returned null or empty result.");
        }
        catch (Exception ex)
        {
            _log.LogMessage(MessageImportance.Low, "Exception trying to call QueryAllTaskItems: {0}", ex.Message);
        }

        // Fallback: try to use the host object directly as IEnumerable<ITaskItem> (legacy behavior).
        if (_hostObject is IEnumerable<ITaskItem> enumerableHost)
        {
            _log.LogMessage(MessageImportance.Low, "Falling back to IEnumerable<ITaskItem> host object.");
            return enumerableHost;
        }

        return null;

        static TaskItem ConvertToTaskItem(TaskItemDto dto)
        {
            TaskItem taskItem = new(dto.ItemSpec ?? string.Empty);
            if (dto.Metadata is not null)
            {
                if (dto.Metadata.TryGetValue(UserMetaDataName, out string? userName))
                {
                    taskItem.SetMetadata(UserMetaDataName, userName);
                }

                if (dto.Metadata.TryGetValue(PasswordMetaDataName, out string? password))
                {
                    taskItem.SetMetadata(PasswordMetaDataName, password);
                }
            }

            return taskItem;
        }
    }

    private readonly record struct TaskItemDto(string? ItemSpec, Dictionary<string, string>? Metadata);
}

