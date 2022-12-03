// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;

namespace Microsoft.DotNet.ApiSymbolExtensions.Logging
{
    public interface ILog
    {
        void LogError(string code, string format, params string[] args);
        void LogWarning(string code, string format, params string[] args);
        void LogMessage(MessageImportance importance, string format, params string[] args);
    }
}
