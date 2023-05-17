// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    /// <summary>
    /// Class for logging information about what happens in the server and client parts of the
    /// Razor command line compiler and build tasks. Useful for debugging what is going on.
    /// </summary>
    /// <remarks>
    /// To use the logging, set the environment variable RAZORBUILDSERVER_LOG to the name
    /// of a file to log to. This file is logged to by both client and server components.
    /// </remarks>
    internal class ServerLogger
    {
        // Environment variable, if set, to enable logging and set the file to log to.
        private const string EnvironmentVariable = "RAZORBUILDSERVER_LOG";

        private static readonly Stream s_loggingStream;
        private static string s_prefix = "---";

        /// <summary>
        /// Static class initializer that initializes logging.
        /// </summary>
        static ServerLogger()
        {
            s_loggingStream = null;

            try
            {
                // Check if the environment
                var loggingFileName = Environment.GetEnvironmentVariable(EnvironmentVariable);

                if (loggingFileName != null)
                {
                    IsLoggingEnabled = true;

                    // If the environment variable contains the path of a currently existing directory,
                    // then use a process-specific name for the log file and put it in that directory.
                    // Otherwise, assume that the environment variable specifies the name of the log file.
                    if (Directory.Exists(loggingFileName))
                    {
                        loggingFileName = Path.Combine(loggingFileName, $"razorserver.{GetCurrentProcessId()}.log");
                    }

                    // Open allowing sharing. We allow multiple processes to log to the same file, so we use share mode to allow that.
                    s_loggingStream = new FileStream(loggingFileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
                }
            }
            catch (Exception e)
            {
                LogException(e, "Failed to create logging stream");
            }
        }

        public static bool IsLoggingEnabled { get; }

        /// <summary>
        /// Set the logging prefix that describes our role.
        /// Typically a 3-letter abbreviation. If logging happens before this, it's logged with "---".
        /// </summary>
        public static void Initialize(string outputPrefix)
        {
            s_prefix = outputPrefix;
        }

        /// <summary>
        /// Log an exception. Also logs information about inner exceptions.
        /// </summary>
        public static void LogException(Exception e, string reason)
        {
            if (IsLoggingEnabled)
            {
                Log("Exception '{0}' occurred during '{1}'. Stack trace:\r\n{2}", e.Message, reason, e.StackTrace);

                var innerExceptionLevel = 0;

                e = e.InnerException;
                while (e != null)
                {
                    Log("Inner exception[{0}] '{1}'. Stack trace: \r\n{1}", innerExceptionLevel, e.Message, e.StackTrace);
                    e = e.InnerException;
                    innerExceptionLevel += 1;
                }
            }
        }

        /// <summary>
        /// Log a line of text to the logging file, with string.Format arguments.
        /// </summary>
        public static void Log(string format, params object[] arguments)
        {
            if (IsLoggingEnabled)
            {
                Log(string.Format(CultureInfo.InvariantCulture, format, arguments));
            }
        }

        /// <summary>
        /// Log a line of text to the logging file.
        /// </summary>
        /// <param name="message"></param>
        public static void Log(string message)
        {
            if (IsLoggingEnabled)
            {
                var prefix = string.Format(CultureInfo.InvariantCulture, "{0} PID={1} TID={2} Ticks={3}: ", s_prefix,
                    GetCurrentProcessId(), Environment.CurrentManagedThreadId, Environment.TickCount);

                var output = prefix + message + "\r\n";
                var bytes = Encoding.UTF8.GetBytes(output);

                // Because multiple processes might be logging to the same file, we always seek to the end,
                // write, and flush.
                s_loggingStream.Seek(0, SeekOrigin.End);
                s_loggingStream.Write(bytes, 0, bytes.Length);
                s_loggingStream.Flush();
            }
        }

        private static int GetCurrentProcessId() =>
#if NET5_0_OR_GREATER
            Environment.ProcessId;
#else
            Process.GetCurrentProcess().Id;
#endif
    }
}
