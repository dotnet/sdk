// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy
{
    public enum DeployStatus
    {
        Unknown = -1,
        Pending = 0,
        Building = 1,
        Deploying = 2,
        Failed = 3,
        Success = 4
    }
}
