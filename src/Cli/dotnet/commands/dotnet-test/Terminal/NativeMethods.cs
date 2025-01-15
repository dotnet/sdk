// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal static class NativeMethods
{
    internal const uint FILE_TYPE_CHAR = 0x0002;
    internal const int STD_OUTPUT_HANDLE = -11;
    internal const int STD_ERROR_HANDLE = -12;
    internal const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    private static bool? s_isWindows;

    /// <summary>
    /// Gets a value indicating whether we are running under some version of Windows.
    /// </summary>
    // TODO: [SupportedOSPlatformGuard("windows")]
    internal static bool IsWindows
    {
        get
        {
            s_isWindows ??= RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            return s_isWindows.Value;
        }
    }

    internal static (bool AcceptAnsiColorCodes, bool OutputIsScreen, uint? OriginalConsoleMode) QueryIsScreenAndTryEnableAnsiColorCodes(StreamHandleType handleType = StreamHandleType.StdOut)
    {
        if (Console.IsOutputRedirected)
        {
            // There's no ANSI terminal support if console output is redirected.
            return (AcceptAnsiColorCodes: false, OutputIsScreen: false, OriginalConsoleMode: null);
        }

        bool acceptAnsiColorCodes = false;
        bool outputIsScreen = false;
        uint? originalConsoleMode = null;
        if (IsWindows)
        {
            try
            {
                nint outputStream = GetStdHandle((int)handleType);
                if (GetConsoleMode(outputStream, out uint consoleMode))
                {
                    if ((consoleMode & ENABLE_VIRTUAL_TERMINAL_PROCESSING) == ENABLE_VIRTUAL_TERMINAL_PROCESSING)
                    {
                        // Console is already in required state.
                        acceptAnsiColorCodes = true;
                    }
                    else
                    {
                        originalConsoleMode = consoleMode;
                        consoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
                        if (SetConsoleMode(outputStream, consoleMode) && GetConsoleMode(outputStream, out consoleMode))
                        {
                            // We only know if vt100 is supported if the previous call actually set the new flag, older
                            // systems ignore the setting.
                            acceptAnsiColorCodes = (consoleMode & ENABLE_VIRTUAL_TERMINAL_PROCESSING) == ENABLE_VIRTUAL_TERMINAL_PROCESSING;
                        }
                    }

                    uint fileType = GetFileType(outputStream);
                    // The std out is a char type (LPT or Console).
                    outputIsScreen = fileType == FILE_TYPE_CHAR;
                    acceptAnsiColorCodes &= outputIsScreen;
                }
            }
            catch
            {
                // In the unlikely case that the above fails we just ignore and continue.
            }
        }
        else
        {
            // On posix OSes detect whether the terminal supports VT100 from the value of the TERM environment variable.
#pragma warning disable RS0030 // Do not use banned APIs
            acceptAnsiColorCodes = AnsiDetector.IsAnsiSupported(Environment.GetEnvironmentVariable("TERM"));
#pragma warning restore RS0030 // Do not use banned APIs
            // It wasn't redirected as tested above so we assume output is screen/console
            outputIsScreen = true;
        }

        return (acceptAnsiColorCodes, outputIsScreen, originalConsoleMode);
    }

    internal static void RestoreConsoleMode(uint? originalConsoleMode, StreamHandleType handleType = StreamHandleType.StdOut)
    {
        if (IsWindows && originalConsoleMode is not null)
        {
            nint stdOut = GetStdHandle((int)handleType);
            _ = SetConsoleMode(stdOut, originalConsoleMode.Value);
        }
    }

    [DllImport("kernel32.dll")]
    // TODO: [SupportedOSPlatform("windows")]
    internal static extern nint GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    // TODO: [SupportedOSPlatform("windows")]
    internal static extern uint GetFileType(nint hFile);

    internal enum StreamHandleType
    {
        /// <summary>
        /// StdOut.
        /// </summary>
        StdOut = STD_OUTPUT_HANDLE,

        /// <summary>
        /// StdError.
        /// </summary>
        StdErr = STD_ERROR_HANDLE,
    }

    [DllImport("kernel32.dll")]
    internal static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll")]
    internal static extern bool SetConsoleMode(nint hConsoleHandle, uint dwMode);
}
