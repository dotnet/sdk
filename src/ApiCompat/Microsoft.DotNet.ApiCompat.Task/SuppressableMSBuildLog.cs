// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.DotNet.ApiCompat.Task
{
    internal class SuppressableMSBuildLog : MSBuildLog, ISuppressableLog
    {
        private readonly ISuppressionEngine _suppressionEngine;
        public SuppressableMSBuildLog(Logger log, ISuppressionEngine suppressionEngine) : base(log)
        {
            _suppressionEngine = suppressionEngine;
        }
        public bool SuppressionWasLogged { get; private set; }

        public bool LogError(Suppression suppression, string code, string format, params string[] args) => LogSuppressableMessage(MessageLevel.Error, suppression, code, format, args);
        public bool LogWarning(Suppression suppression, string code, string format, params string[] args) => LogSuppressableMessage(MessageLevel.Warning, suppression, code, format, args);
        private bool LogSuppressableMessage(MessageLevel messageLevel, Suppression suppression, string code, string format, params string[] args)
        {
            if (_suppressionEngine.IsErrorSuppressed(suppression))
            {
                return false;
            }
            SuppressionWasLogged = true;
            _log.Log(new Message(messageLevel, string.Format(format, args), code));
            return true;
        }
    }
}
