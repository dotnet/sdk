// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    internal class VSMsDeployTaskHostObject : ITaskHost, IEnumerable<ITaskItem>
    {
        private List<TaskItem> _items;

        public static string CredentialItemSpecName = "MsDeployCredential";
        public static string UserMetaDataName = "UserName";
        public static string PasswordMetaDataName = "Password";
        public static string SkipFileItemSpecName = "MsDeploySkipFile";
        public static string SourceDeployObject = "Source";
        public static string DestinationDeployObject = "Destination";
        public static string SkipApplyMetadataName = "Apply";

        public VSMsDeployTaskHostObject()
        {
            _items = new List<TaskItem>();
        }

        public List<TaskItem> GetTaskItems()
        {
            return _items;
        }

        public void AddCredentialTaskItemIfExists(string userName, string password)
        {
            if (!string.IsNullOrEmpty(userName))
            {
                TaskItem credentialItem = new(CredentialItemSpecName);
                ITaskItem2 iTaskItem2 = (credentialItem as ITaskItem2);
                iTaskItem2.SetMetadataValueLiteral(UserMetaDataName, userName);
                iTaskItem2.SetMetadataValueLiteral(PasswordMetaDataName, password);
                _items.Add(credentialItem);
            }
        }

        public void AddFileSkips(List<FileSkipData> fileSkipInfos, /*key is src relative path, value is full destination path*/
            string rootFolderOfFileToPublish)
        {
            foreach (FileSkipData p in fileSkipInfos)
            {
                TaskItem srcSkipRuleItem = new(SkipFileItemSpecName);
                srcSkipRuleItem.SetMetadata("ObjectName", p.SourceProvider);
                if (p.SourceFilePath is not null)
                {
                    srcSkipRuleItem.SetMetadata("AbsolutePath", Regex.Escape(Path.Combine(rootFolderOfFileToPublish, p.SourceFilePath)) + "$");
                }
                srcSkipRuleItem.SetMetadata(SkipApplyMetadataName, SourceDeployObject);
                _items.Add(srcSkipRuleItem);

                TaskItem destSkipRuleItem = new(SkipFileItemSpecName);
                destSkipRuleItem.SetMetadata("ObjectName", p.DestinationProvider);
                if (p.DestinationFilePath is not null)
                {
                    destSkipRuleItem.SetMetadata("AbsolutePath", Regex.Escape(p.DestinationFilePath) + "$");
                }
                destSkipRuleItem.SetMetadata(SkipApplyMetadataName, DestinationDeployObject);
                _items.Add(destSkipRuleItem);
            }
        }

        public IEnumerator<ITaskItem> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_items).GetEnumerator();
        }
    }
}
