// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiCompat.Tool
{
    internal class SuppressableConsoleLog : ConsoleLog, ISuppressableLog
    {
        private readonly ISuppressionEngine _suppressionEngine;
        public bool SuppressionWasLogged { get; private set; }

        public SuppressableConsoleLog(ISuppressionEngine suppressionEngine, MessageImportance messageImportance) : base(messageImportance)
        {
            _suppressionEngine = suppressionEngine;
        }

        public bool LogError(Suppression suppression, string code, string format, params string[] args) => LogSuppressableMessage(Console.Error, suppression, code, format, args);
        public bool LogWarning(Suppression suppression, string code, string format, params string[] args) => LogSuppressableMessage(Console.Out, suppression, code, format, args);

        private bool LogSuppressableMessage(TextWriter textWriter, Suppression suppression, string code, string format, params string[] args)
        {
            if (_suppressionEngine.IsErrorSuppressed(suppression))
            {
                return false;
            }
            SuppressionWasLogged = true;
            textWriter.WriteLine($"{code}: {string.Format(format, args)}");
            return true;
        }
    }
}
