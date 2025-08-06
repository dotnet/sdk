// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.CommandLine;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestCommand : RestoringCommand
    {
        public TestCommand(
            IEnumerable<string> msbuildArgs,
            bool noRestore,
            string msbuildPath = null)
            : base(msbuildArgs, noRestore, msbuildPath)
        {
        }

        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            FeatureFlag.Instance.PrintFlagFeatureState();

            // We use also current process id for the correlation id for possible future usage in case we need to know the parent process
            // from the VSTest side.
            string testSessionCorrelationId = $"{Environment.ProcessId}_{Guid.NewGuid()}";

            string[] args = parseResult.GetArguments();

            if (VSTestTrace.TraceEnabled)
            {
                string commandLineParameters = "";
                if (args?.Length > 0)
                {
                    commandLineParameters = args.Aggregate((a, b) => $"{a} | {b}");
                }
                VSTestTrace.SafeWriteTrace(() => $"Argument list: '{commandLineParameters}'");
            }

            // settings parameters are after -- (including --), these should not be considered by the parser
            string[] settings = args.SkipWhile(a => a != "--").ToArray();
            // all parameters before --
            args = args.TakeWhile(a => a != "--").ToArray();

            // Fix for https://github.com/Microsoft/vstest/issues/1453
            // Run dll/exe directly using the VSTestForwardingApp
            if (ContainsBuiltTestSources(args))
            {
                return ForwardToVSTestConsole(parseResult, args, settings, testSessionCorrelationId);
            }

            return ForwardToMsbuild(parseResult, settings, testSessionCorrelationId);
        }

        private static int ForwardToMsbuild(ParseResult parseResult, string[] settings, string testSessionCorrelationId)
        {
            // Workaround for https://github.com/Microsoft/vstest/issues/1503
            const string NodeWindowEnvironmentName = "MSBUILDENSURESTDOUTFORTASKPROCESSES";
            string previousNodeWindowSetting = Environment.GetEnvironmentVariable(NodeWindowEnvironmentName);
            try
            {
                var forceLegacyOutput = previousNodeWindowSetting == "1";
                var properties = GetUserSpecifiedExplicitMSBuildProperties(parseResult);
                var hasUserMSBuildOutputProperty = properties.TryGetValue("VsTestUseMSBuildOutput", out var propertyValue);

                string[] additionalBuildProperties;

                var useTerminalLogger = TerminalLoggerDetector.ProcessTerminalLoggerConfiguration(parseResult);

                if (useTerminalLogger == TerminalLoggerMode.Invalid)
                {
                    // TL option is invalid we want terminal logger to fail in its own way and don't want to disable it.
                    // Do noting.
                    additionalBuildProperties = Array.Empty<string>();
                }
                else if (forceLegacyOutput)
                {
                    additionalBuildProperties = SetLegacyVSTestWorkarounds(NodeWindowEnvironmentName);
                }
                else if (useTerminalLogger == TerminalLoggerMode.Off)
                {
                    additionalBuildProperties = SetLegacyVSTestWorkarounds(NodeWindowEnvironmentName);
                }
                else if (hasUserMSBuildOutputProperty)
                {                   
                    if (propertyValue.ToLowerInvariant() == "false")
                    {
                        additionalBuildProperties = SetLegacyVSTestWorkarounds(NodeWindowEnvironmentName);
                    }
                    else
                    {
                        // the property is already present don't add it.
                        additionalBuildProperties = Array.Empty<string>();
                    }
                }
                else
                {
                    // Enable TL mode.
                    additionalBuildProperties = ["--property:VsTestUseMSBuildOutput=true"];
                }

                int exitCode = FromParseResult(parseResult, settings, testSessionCorrelationId, additionalBuildProperties).Execute();

                // We run post processing also if execution is failed for possible partial successful result to post process.
                exitCode |= RunArtifactPostProcessingIfNeeded(testSessionCorrelationId, parseResult, FeatureFlag.Instance);

                return exitCode;
            }
            finally
            {
                Environment.SetEnvironmentVariable(NodeWindowEnvironmentName, previousNodeWindowSetting);
            }

            static string[] SetLegacyVSTestWorkarounds(string NodeWindowEnvironmentName)
            {
                string[] additionalBuildProperties;
                // User explicitly disabled the new logger. Use workarounds needed for old logger.
                // Workaround for https://github.com/Microsoft/vstest/issues/1503
                Environment.SetEnvironmentVariable(NodeWindowEnvironmentName, "1");
                additionalBuildProperties = ["-nodereuse:false"];
                return additionalBuildProperties;
            }
        }

        private static int ForwardToVSTestConsole(ParseResult parseResult, string[] args, string[] settings, string testSessionCorrelationId)
        {
            List<string> convertedArgs = new VSTestArgumentConverter().Convert(args, out List<string> ignoredArgs);
            if (ignoredArgs.Any())
            {
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.IgnoredArgumentsMessage, string.Join(" ", ignoredArgs)).Yellow());
            }

            // merge the args settings, we don't need to escape
            // one more time, there is no extra hop via msbuild
            convertedArgs.AddRange(settings);

            if (!FeatureFlag.Instance.IsSet(FeatureFlag.DISABLE_ARTIFACTS_POSTPROCESSING))
            {
                // Add artifacts processing mode and test session id for the artifact post-processing
                convertedArgs.Add("--artifactsProcessingMode-collect");
                convertedArgs.Add($"--testSessionCorrelationId:{testSessionCorrelationId}");
            }

            int exitCode = new VSTestForwardingApp(convertedArgs).Execute();

            // We run post processing also if execution is failed for possible partial successful result to post process.
            exitCode |= RunArtifactPostProcessingIfNeeded(testSessionCorrelationId, parseResult, FeatureFlag.Instance);

            return exitCode;
        }

        public static TestCommand FromArgs(string[] args, string testSessionCorrelationId = null, string msbuildPath = null)
        {
            var parser = Parser.Instance;
            var parseResult = parser.ParseFrom("dotnet test", args);

            // settings parameters are after -- (including --), these should not be considered by the parser
            string[] settings = args.SkipWhile(a => a != "--").ToArray();
            if (string.IsNullOrEmpty(testSessionCorrelationId))
            {
                testSessionCorrelationId = $"{Environment.ProcessId}_{Guid.NewGuid()}";
            }

            return FromParseResult(parseResult, settings, testSessionCorrelationId, Array.Empty<string>(), msbuildPath);
        }

        private static TestCommand FromParseResult(ParseResult result, string[] settings, string testSessionCorrelationId, string[] additionalBuildProperties, string msbuildPath = null)
        {
            result.ShowHelpOrErrorIfAppropriate();

            // Extra msbuild properties won't be parsed and so end up in the UnmatchedTokens list. In addition to those
            // properties, all the test settings properties are also considered as unmatched but we don't want to forward
            // these as-is to msbuild. So we filter out the test settings properties from the unmatched tokens,
            // by only taking values until the first item after `--`. (`--` is not present in the UnmatchedTokens).
            var unMatchedNonSettingsArgs = settings.Length > 1
                ? result.UnmatchedTokens.TakeWhile(x => x != settings[1])
                : result.UnmatchedTokens;

            var parsedArgs =
                result.OptionValuesToBeForwarded(TestCommandParser.GetCommand()) // all msbuild-recognized tokens
                    .Concat(unMatchedNonSettingsArgs); // all tokens that the test-parser doesn't explicitly track (minus the settings tokens)

            VSTestTrace.SafeWriteTrace(() => $"MSBuild args from forwarded options: {string.Join(", ", parsedArgs)}");

            var msbuildArgs = new List<string>(additionalBuildProperties)
            {
                "-target:VSTest",
                "-nologo",
            };

            msbuildArgs.AddRange(parsedArgs);

            if (settings.Any())
            {
                // skip '--' and escape every \ to be \\ and every " to be \" to survive the next hop
                string[] escaped = settings.Skip(1).Select(s => s.Replace("\\", "\\\\").Replace("\"", "\\\"")).ToArray();

                string runSettingsArg = string.Join(";", escaped);
                msbuildArgs.Add($"-property:VSTestCLIRunSettings=\"{runSettingsArg}\"");
            }

            string verbosityArg = result.ForwardedOptionValues<IReadOnlyCollection<string>>(TestCommandParser.GetCommand(), "--verbosity")?.SingleOrDefault() ?? null;
            if (verbosityArg != null)
            {
                string[] verbosity = verbosityArg.Split(':', 2);
                if (verbosity.Length == 2)
                {
                    msbuildArgs.Add($"-property:VSTestVerbosity={verbosity[1]}");
                }
            }

            if (!FeatureFlag.Instance.IsSet(FeatureFlag.DISABLE_ARTIFACTS_POSTPROCESSING))
            {
                // Add artifacts processing mode and test session id for the artifact post-processing
                msbuildArgs.Add("-property:VSTestArtifactsProcessingMode=collect");
                msbuildArgs.Add($"-property:VSTestSessionCorrelationId={testSessionCorrelationId}");
            }

            bool noRestore = (result.GetResult(TestCommandParser.NoRestoreOption) ?? result.GetResult(TestCommandParser.NoBuildOption)) is not null;

            TestCommand testCommand = new(
                msbuildArgs,
                noRestore,
                msbuildPath);

            // Apply environment variables provided by the user via --environment (-e) option, if present
            if (result.GetValue(CommonOptions.EnvOption) is { } environmentVariables)
            {
                foreach (var (name, value) in environmentVariables)
                {
                    testCommand.EnvironmentVariable(name, value);
                }
            }

            // Set DOTNET_PATH if it isn't already set in the environment as it is required
            // by the testhost which uses the apphost feature (Windows only).
            (bool hasRootVariable, string rootVariableName, string rootValue) = VSTestForwardingApp.GetRootVariable();
            if (!hasRootVariable)
            {
                testCommand.EnvironmentVariable(rootVariableName, rootValue);
                VSTestTrace.SafeWriteTrace(() => $"Root variable set {rootVariableName}:{rootValue}");
            }

            VSTestTrace.SafeWriteTrace(() => $"Starting test using MSBuild with arguments '{testCommand.GetArgumentsToMSBuild()}' custom MSBuild path '{msbuildPath}' norestore '{noRestore}'");
            return testCommand;
        }

        internal static int RunArtifactPostProcessingIfNeeded(string testSessionCorrelationId, ParseResult parseResult, FeatureFlag disableFeatureFlag)
        {
            if (disableFeatureFlag.IsSet(FeatureFlag.DISABLE_ARTIFACTS_POSTPROCESSING))
            {
                return 0;
            }

            // VSTest runner will save artifacts inside a temp folder if needed.
            string expectedArtifactDirectory = Path.Combine(Path.GetTempPath(), testSessionCorrelationId);
            if (!Directory.Exists(expectedArtifactDirectory))
            {
                VSTestTrace.SafeWriteTrace(() => "No artifact found, post-processing won't run.");
                return 0;
            }

            VSTestTrace.SafeWriteTrace(() => $"Artifacts directory found '{expectedArtifactDirectory}', running post-processing.");

            var artifactsPostProcessArgs = new List<string> { "--artifactsProcessingMode-postprocess", $"--testSessionCorrelationId:{testSessionCorrelationId}" };

            if (parseResult.GetResult(TestCommandParser.DiagOption) is not null)
            {
                artifactsPostProcessArgs.Add($"--diag:{parseResult.GetValue(TestCommandParser.DiagOption)}");
            }

            try
            {
                return new VSTestForwardingApp(artifactsPostProcessArgs).Execute();
            }
            finally
            {
                if (Directory.Exists(expectedArtifactDirectory))
                {
                    VSTestTrace.SafeWriteTrace(() => $"Cleaning artifact directory '{expectedArtifactDirectory}'.");
                    try
                    {
                        Directory.Delete(expectedArtifactDirectory, true);
                    }
                    catch (Exception ex)
                    {
                        VSTestTrace.SafeWriteTrace(() => $"Exception during artifact cleanup: \n{ex}");
                    }
                }
            }
        }

        private static bool ContainsBuiltTestSources(string[] args)
        {
            foreach (string arg in args)
            {
                if (!arg.StartsWith("-") &&
                    (arg.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || arg.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <returns>A case-insensitive dictionary of any properties passed from the user and their values.</returns>
        private static Dictionary<string, string> GetUserSpecifiedExplicitMSBuildProperties(ParseResult parseResult)
        {
            Dictionary<string, string> globalProperties = new(StringComparer.OrdinalIgnoreCase);
            IEnumerable<string> globalPropEnumerable = parseResult.UnmatchedTokens;
            foreach (var unmatchedToken in globalPropEnumerable)
            {
                var propertyPairs = MSBuildPropertyParser.ParseProperties(unmatchedToken);
                foreach (var propertyKeyValue in propertyPairs)
                {
                    string propertyName;
                    if (propertyKeyValue.key.StartsWith("--property:", StringComparison.OrdinalIgnoreCase)
                        || propertyKeyValue.key.StartsWith("/property:", StringComparison.OrdinalIgnoreCase))
                    {
                        propertyName = propertyKeyValue.key.RemovePrefix().Substring("property:".Length);
                    }
                    else if (propertyKeyValue.key.StartsWith("-p:", StringComparison.OrdinalIgnoreCase)
                        || propertyKeyValue.key.StartsWith("/p:", StringComparison.OrdinalIgnoreCase))
                    {
                        propertyName = propertyKeyValue.key.RemovePrefix().Substring("p:".Length);
                    }
                    else
                    {
                        continue;
                    }

                    globalProperties[propertyName] = propertyKeyValue.value;
                }
            }
            return globalProperties;
        }
    }

    public class TerminalLoggerDetector
    {
        public static TerminalLoggerMode ProcessTerminalLoggerConfiguration(ParseResult parseResult)
        {
            string terminalLoggerArg = null;
            if (!TryFromCommandLine(parseResult.UnmatchedTokens, out terminalLoggerArg) && !TryFromEnvironmentVariables(out terminalLoggerArg))
            {
                terminalLoggerArg = FindDefaultValue(parseResult.UnmatchedTokens) ?? "auto";
            }

            terminalLoggerArg = NormalizeIntoBooleanValues(terminalLoggerArg!);

            TerminalLoggerMode useTerminalLogger = TerminalLoggerMode.Off;
            if (bool.TryParse(terminalLoggerArg, out bool boolOption))
            {
                // When true, terminal logger will be forced, when false it won't be used.
                useTerminalLogger = boolOption ? TerminalLoggerMode.On : TerminalLoggerMode.Off;
            }
            else
            {
                //  When we could not parse the value to bool. It can be either "auto" or invalid.
                if (!terminalLoggerArg.Equals("auto", StringComparison.OrdinalIgnoreCase))
                {
                    // Value is not one of: true (or on), false (or off) or auto, MSBuild should fail.
                    // We should not return false, because that will suppress TerminalLogger from trying to setup.
                    useTerminalLogger = TerminalLoggerMode.Invalid;
                }
                else
                {
                    useTerminalLogger = CheckIfTerminalIsSupportedAndTryEnableAnsiColorCodes() ? TerminalLoggerMode.On : TerminalLoggerMode.Off;
                }
            }

            return useTerminalLogger;

            static bool CheckIfTerminalIsSupportedAndTryEnableAnsiColorCodes()
            {
                if (Environment.GetEnvironmentVariable("MSBUILDENSURESTDOUTFORTASKPROCESSES") == "1")
                {
                    return false;
                }

                (var acceptAnsiColorCodes, var outputIsScreen, var originalConsoleMode) = NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes();
                if (originalConsoleMode != null)
                {
                    // Restore to previous state, so MSBuild can set it themselves.
                    NativeMethods.RestoreConsoleMode(originalConsoleMode);
                }

                if (!outputIsScreen)
                {
                    return false;
                }

                // TerminalLogger is not used if the terminal does not support ANSI/VT100 escape sequences.
                if (!acceptAnsiColorCodes)
                {
                    return false;
                }

                return true;
            }

            string FindDefaultValue(IReadOnlyList<string> unmatchedTokens)
            {
                // Find default configuration so it is part of telemetry even when default is not used.
                // Default can be stored in /tlp:default=true|false|on|off|auto
                Switch terminalLoggerDefault = Find(unmatchedTokens, "tlp", "terminalloggerparameters");
                if (terminalLoggerDefault == null)
                {
                    return null;
                }

                if (terminalLoggerDefault.Value == null)
                {
                    return null;
                }

                foreach (string parameter in terminalLoggerDefault.Value.Split(':'))
                {
                    if (string.IsNullOrWhiteSpace(parameter))
                    {
                        continue;
                    }

                    string[] parameterAndValue = parameter.Split('=');
                    if (parameterAndValue[0].Equals("default", StringComparison.InvariantCultureIgnoreCase) && parameterAndValue.Length > 1)
                    {
                        return parameterAndValue[1];
                    }
                }

                return null;
            }

            bool TryFromCommandLine(IReadOnlyList<string> unmatchedTokens, out string value)
            {
                Switch terminalLogger = Find(unmatchedTokens, ["tl", "terminalLogger", "ll", "livelogger"]);
                if (terminalLogger == null)
                {
                    value = null;
                    return false;
                }

                if (terminalLogger.Value == null)
                {
                    // if the switch was set but not to an explicit value, the value is "auto"
                    value = "auto";
                    return true;
                }

                value = terminalLogger.Value;
                return true;
            }

            bool TryFromEnvironmentVariables(out string terminalLoggerArg)
            {
                // Keep MSBUILDLIVELOGGER supporting existing use. But MSBUILDTERMINALLOGGER takes precedence.
                string liveLoggerArg = Environment.GetEnvironmentVariable("MSBUILDLIVELOGGER");
                terminalLoggerArg = Environment.GetEnvironmentVariable("MSBUILDTERMINALLOGGER");
                if (!string.IsNullOrEmpty(terminalLoggerArg))
                {
                    return true;
                }
                else if (!string.IsNullOrEmpty(liveLoggerArg))
                {
                    terminalLoggerArg = liveLoggerArg;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            string NormalizeIntoBooleanValues(string terminalLoggerArg)
            {
                // We now have a string`. It can be "true" or "false" which means just that:
                if (terminalLoggerArg.Equals("on", StringComparison.InvariantCultureIgnoreCase))
                {
                    terminalLoggerArg = bool.TrueString;
                }
                else if (terminalLoggerArg.Equals("off", StringComparison.InvariantCultureIgnoreCase))
                {
                    terminalLoggerArg = bool.FalseString;
                }

                return terminalLoggerArg;
            }
        }

        private static Switch Find(IReadOnlyList<string> unmatchedTokens, params string[] names)
        {
            foreach (string prefix in new string[] { "-", "--", "/" })
            {
                foreach (var name in names)
                {
                    var found = unmatchedTokens.FirstOrDefault(t => t.StartsWith(prefix + name, StringComparison.OrdinalIgnoreCase));
                    if (found != null)
                    {
                        var param = found.Substring(prefix.Length);
                        if (!param.Contains(":"))
                        {
                            return new Switch(param, null);
                        }
                        else
                        {
                            var parts = param.Split(":", 2);
                            return new Switch(parts[0], parts[1]);
                        }
                    }
                }
            }

            return null;
        }

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
            [SupportedOSPlatformGuard("windows")]
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
                if (System.Console.IsOutputRedirected)
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
            [SupportedOSPlatform("windows")]
            internal static extern nint GetStdHandle(int nStdHandle);

            [DllImport("kernel32.dll")]
            [SupportedOSPlatform("windows")]
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

        internal static class AnsiDetector
        {
            private static readonly Regex[] TerminalsRegexes =
            {
        new("^xterm"), // xterm, PuTTY, Mintty
        new("^rxvt"), // RXVT
        new("^(?!eterm-color).*eterm.*"), // Accepts eterm, but not eterm-color, which does not support moving the cursor, see #9950.
        new("^screen"), // GNU screen, tmux
        new("tmux"), // tmux
        new("^vt100"), // DEC VT series
        new("^vt102"), // DEC VT series
        new("^vt220"), // DEC VT series
        new("^vt320"), // DEC VT series
        new("ansi"), // ANSI
        new("scoansi"), // SCO ANSI
        new("cygwin"), // Cygwin, MinGW
        new("linux"), // Linux console
        new("konsole"), // Konsole
        new("bvterm"), // Bitvise SSH Client
        new("^st-256color"), // Suckless Simple Terminal, st
        new("alacritty"), // Alacritty
    };

            public static bool IsAnsiSupported(string termType)
                => !String.IsNullOrEmpty(termType) && TerminalsRegexes.Any(regex => regex.IsMatch(termType));
        }

        private record class Switch(string Name, string Value);
    }

    public enum TerminalLoggerMode
    {
        Off,
        On,
        Invalid
    }
}
