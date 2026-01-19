// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;

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
            // !!! Should be in sync with the implementation in Microsoft.WebTools.Publish.MSDeploy.VSMsDeployTaskHostObject
            string? rawTaskItems = (string?)_hostObject!.GetType().InvokeMember(
                "QueryAllTaskItems",
                BindingFlags.InvokeMethod,
                null,
                _hostObject,
                null);

            if (!string.IsNullOrEmpty(rawTaskItems))
            {
                List<TaskItemDto>? dtos = JsonConvert.DeserializeObject<List<TaskItemDto>>(rawTaskItems);
                if (dtos is not null)
                {
                    return dtos.Select(ConvertToTaskItem).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogMessage(MessageImportance.Low, "Exception trying to call QueryAllTaskItems: {0}", ex.Message);
        }

        return _hostObject as IEnumerable<ITaskItem>;

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

    private readonly record struct TaskItemDto(string ItemSpec, Dictionary<string, string> Metadata);
}
