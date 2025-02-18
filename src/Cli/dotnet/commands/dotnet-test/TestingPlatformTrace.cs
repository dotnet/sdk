// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Test
{
    internal class TestingPlatformTrace
    {
        public static bool TraceEnabled { get; private set; }
        private static readonly string _traceFilePath;
        private static readonly object _lock = new();

        static TestingPlatformTrace()
        {
            _traceFilePath = Environment.GetEnvironmentVariable("DOTNET_CLI_TESTING_PLATFORM_TRACEFILE");
            TraceEnabled = !string.IsNullOrEmpty(_traceFilePath);
        }

        public static void Write(Func<string> messageLog)
        {
            if (!TraceEnabled)
            {
                return;
            }

            try
            {
                string message = $"[dotnet test - {DateTimeOffset.UtcNow.ToString(format: "MM/dd/yyyy HH:mm:ss.fff")}]{messageLog()}";

                lock (_lock)
                {
                    using StreamWriter logFile = File.AppendText(_traceFilePath);
                    logFile.WriteLine(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[dotnet test - {DateTimeOffset.UtcNow}]{ex}");
            }
        }
    }
}
