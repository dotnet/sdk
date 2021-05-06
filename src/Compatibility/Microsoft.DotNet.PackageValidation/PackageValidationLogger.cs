// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using NuGet.Common;

namespace Microsoft.DotNet.PackageValidation
{
    public class PackageValidationLogger : ILogger
    {
        public void Log(LogLevel level, string data) => throw new NotImplementedException();
        public void Log(ILogMessage message) => throw new NotImplementedException();
        public Task LogAsync(LogLevel level, string data) => throw new NotImplementedException();
        public Task LogAsync(ILogMessage message) => throw new NotImplementedException();
        public void LogDebug(string data) => throw new NotImplementedException();
        public void LogInformation(string data) => throw new NotImplementedException();
        public void LogInformationSummary(string data) => throw new NotImplementedException();
        public void LogMinimal(string data) => throw new NotImplementedException();
        public void LogVerbose(string data) => throw new NotImplementedException();
        public void LogWarning(string data) => throw new NotImplementedException();
        public void LogError(string data)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Error] {data}");
            Console.ResetColor();
        }
    }
}
