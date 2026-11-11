// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

///--------------------------------------------------------------------------------------------
/// KuduDeploy.cs
///
/// Support for WAWS deployment using Kudu API.
///
/// Copyright(c) 2006 Microsoft Corporation
///--------------------------------------------------------------------------------------------

using Microsoft.NET.Sdk.Publish.Tasks.Properties;
using Framework = Microsoft.Build.Framework;

namespace Microsoft.NET.Sdk.Publish.Tasks.Kudu
{
    public sealed class KuduDeploy : Task
    {
        public static readonly int TimeoutMilliseconds = 180000;
        [Framework.Required]
        public string? PublishIntermediateOutputPath
        {
            get;
            set;
        }

        [Framework.Required]
        public string? PublishUrl
        {
            get;
            set;
        }

        [Framework.Required]
        public string? UserName
        {
            get;
            set;
        }

        [Framework.Required]
        public string? Password
        {
            get;
            set;
        }

        [Framework.Required]
        public string? PublishSiteName
        {
            get;
            set;
        }

        public bool DeployIndividualFiles
        {
            get;
            set;
        }

        internal KuduConnectionInfo GetConnectionInfo()
        {
            KuduConnectionInfo connectionInfo = new()
            {
                DestinationUrl = PublishUrl,
                UserName = UserName,
                Password = Password,
                SiteName = PublishSiteName
            };

            return connectionInfo;
        }

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(PublishIntermediateOutputPath))
            {
                Log.LogError(Resources.KUDUDEPLOY_DeployOutputPathEmpty);
                return false;
            }

            KuduConnectionInfo connectionInfo = GetConnectionInfo();
            if (string.IsNullOrEmpty(connectionInfo.UserName) || string.IsNullOrEmpty(connectionInfo.Password) || string.IsNullOrEmpty(connectionInfo.DestinationUrl))
            {
                Log.LogError(Resources.KUDUDEPLOY_ConnectionInfoMissing);
                return false;
            }

            bool publishStatus = DeployFiles(connectionInfo);

            if (publishStatus)
            {
                Log.LogMessage(Framework.MessageImportance.High, Resources.KUDUDEPLOY_PublishSucceeded);
            }
            else
            {
                Log.LogError(Resources.KUDUDEPLOY_PublishFailed);
            }

            return publishStatus;
        }

        internal bool DeployFiles(KuduConnectionInfo connectionInfo)
        {
            KuduVfsDeploy fileDeploy = new(connectionInfo, Log);

            bool success;
            if (!DeployIndividualFiles)
            {
                success = DeployZipFile(connectionInfo);
                return success;
            }

            // Deploy the files.
            System.Threading.Tasks.Task? deployTask = fileDeploy.DeployAsync(PublishIntermediateOutputPath);
            try
            {
                success = deployTask?.Wait(TimeoutMilliseconds) ?? false;
                if (!success)
                {
                    Log.LogError(string.Format(Resources.KUDUDEPLOY_AzurePublishErrorReason, Resources.KUDUDEPLOY_OperationTimeout));
                }
            }
            catch (AggregateException ae)
            {
                Log.LogError(string.Format(Resources.KUDUDEPLOY_AzurePublishErrorReason, ae.Flatten().Message));
                success = false;
            }

            return success;
        }

        internal bool DeployZipFile(KuduConnectionInfo connectionInfo)
        {
            if (PublishIntermediateOutputPath is null)
            {
                Log.LogError(Resources.KUDUDEPLOY_DeployOutputPathEmpty);
                return false;
            }

            string? tempSubdirectory = null;
            try
            {
                // Create a temporary directory to store write the zip file.
#if NETFRAMEWORK
                tempSubdirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
#else
                tempSubdirectory = Directory.CreateTempSubdirectory().FullName;
#endif

                // Zip the files from PublishOutput path.
                string zipFileFullPath = Path.Combine(tempSubdirectory, "Publish.zip");
                Log.LogMessage(Framework.MessageImportance.High, string.Format(Resources.KUDUDEPLOY_CopyingToTempLocation, zipFileFullPath));

                System.IO.Compression.ZipFile.CreateFromDirectory(PublishIntermediateOutputPath, zipFileFullPath);
                Log.LogMessage(Framework.MessageImportance.High, Resources.KUDUDEPLOY_CopyingToTempLocationCompleted);

                // Deploy
                KuduZipDeploy zipDeploy = new(connectionInfo, Log);
                Task<bool> zipTask = zipDeploy.DeployAsync(zipFileFullPath);
                bool success = zipTask.Wait(TimeoutMilliseconds);
                if (!success)
                {
                    Log.LogError(string.Format(Resources.KUDUDEPLOY_AzurePublishErrorReason, Resources.KUDUDEPLOY_OperationTimeout));
                }

                return success && zipTask.Result;
            }
            catch (AggregateException ae)
            {
                Log.LogError(string.Format(Resources.KUDUDEPLOY_AzurePublishErrorReason, ae.Flatten().Message));
                return false;
            }
            catch (Exception e)
            {
                Log.LogError(string.Format(Resources.KUDUDEPLOY_AzurePublishErrorReason, e.Message));
                return false;
            }
            finally
            {
                System.Threading.Tasks.Task.Factory.StartNew(
                    () =>
                    {
                        if (tempSubdirectory is not null)
                        {
                            try
                            {
                                Directory.Delete(tempSubdirectory, recursive: true);
                            }
                            catch
                            {
                                // We don't need to do any thing if we are unable to delete the temp file.
                            }
                        }
                    }
                );
            }
        }
    }
}
