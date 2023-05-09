﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    internal class SuppressableTestLog : ISuppressableLog
    {
        public List<string> errors = new();
        public List<string> warnings = new();

        public bool HasLoggedErrors => errors.Count != 0;
        public bool HasLoggedSuppressions { get; private set; }

        public bool LogError(Suppression suppression, string code, string message)
        {
            HasLoggedSuppressions = true;
            errors.Add($"{code} {message}");

            return true;
        }
        public void LogError(string message) => errors.Add(message);
        public void LogError(string code, string message) => errors.Add($"{code} {message}");
        
        public bool LogWarning(Suppression suppression, string code, string message)
        {
            HasLoggedSuppressions = true;
            warnings.Add($"{code} {message}");

            return true;
        }
        public void LogWarning(string message) => warnings.Add(message);
        public void LogWarning(string code, string message) => warnings.Add($"{code} {message}");

        public void LogMessage(string message) { }
        public void LogMessage(MessageImportance importance, string message) { }
    }
}
