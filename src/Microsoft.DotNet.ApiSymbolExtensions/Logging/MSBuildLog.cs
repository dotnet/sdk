// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.DotNet.ApiSymbolExtensions.Logging
{
    public class MSBuildLog : ILog
    {
        protected readonly Logger _log;
        public MSBuildLog(Logger log)
        {
            _log = log;
        }

        public void LogError(string code, string format, params string[] args) => _log.Log(new Message(MessageLevel.Error, string.Format(format, args), code));
        public void LogWarning(string code, string format, params string[] args) => _log.Log(new Message(MessageLevel.Warning, string.Format(format, args), code));
        public void LogMessage(MessageImportance importance, string format, params string[] args) => _log.LogMessage(importance, format, args);
    }
}
