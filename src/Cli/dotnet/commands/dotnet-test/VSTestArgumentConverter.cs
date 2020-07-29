// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Converts the given arguments to vstest parsable arguments
    /// </summary>
    public class VSTestArgumentConverter
    {
        private const string verbosityString = "--logger:console;verbosity=";

        private readonly Dictionary<string, string> ArgumentMapping = new Dictionary<string, string>
        {
            ["-h"] = "--help",
            ["-s"] = "--settings",
            ["-t"] = "--listtests",
            ["-a"] = "--testadapterpath",
            ["-l"] = "--logger",
            ["-f"] = "--framework",
            ["-d"] = "--diag",
            ["--filter"] = "--testcasefilter",
            ["--list-tests"] = "--listtests",
            ["--test-adapter-path"] = "--testadapterpath",
            ["--results-directory"] = "--resultsdirectory"
        };

        private readonly Dictionary<string, string> VerbosityMapping = new Dictionary<string, string>
        {
            ["q"] = "quiet",
            ["m"] = "minimal",
            ["n"] = "normal",
            ["d"] = "detailed",
            ["diag"] = "diagnostic"
        };

        private readonly string[] IgnoredArguments = new string[]
        {
            "-c",
            "--configuration",
            "--runtime",
            "-o",
            "--output",
            "--no-build",
            "--no-restore",
            "--interactive"
        };

        private readonly string _blame = "--blame";
        private readonly string _blameCrash = "--blame-crash";
        private readonly string _blameCrashDumpType = "--blame-crash-dump-type";
        private readonly string _blameCrashCollectAlways = "--blame-crash-collect-always";
        private readonly string _blameHang = "--blame-hang";
        private readonly string _blameHangDumpType = "--blame-hang-dump-type";
        private readonly string _blameHangTimeout = "--blame-hang-timeout";


        /// <summary>
        /// Converts the given arguments to vstest parsable arguments
        /// </summary>
        /// <param name="args">original arguments</param>
        /// <param name="ignoredArgs">arguments ignored by the converter</param>
        /// <returns>list of args which can be passsed to vstest</returns>
        public List<string> Convert(string[] args, out List<string> ignoredArgs)
        {
            var newArgList = new List<string>();
            ignoredArgs = new List<string>();

            string activeArgument = null;

            bool blame = false;

            bool collectCrashDump = false;
            string collectCrashDumpType = null;
            bool collectCrashDumpAlways = false;

            bool collectHangDump = false;
            string collectHangDumpType = null;
            string collectHangDumpTimeout = null;

            foreach (var arg in args)
            {
                if (arg == "--")
                {
                    throw new ArgumentException("Inline settings should not be passed to Convert.");
                }

                if (arg.StartsWith("-"))
                {
                    if (!string.IsNullOrEmpty(activeArgument))
                    {
                        if (IgnoredArguments.Contains(activeArgument))
                        {
                            ignoredArgs.Add(activeArgument);
                        }
                        else
                        {
                            newArgList.Add(activeArgument);
                        }
                        activeArgument = null;
                    }

                    // Check if the arg contains the value separated by colon
                    if (arg.Contains(":"))
                    {
                        var argValues = arg.Split(':');

                        if (IgnoredArguments.Contains(argValues[0]))
                        {
                            ignoredArgs.Add(arg);
                            continue;
                        }

                        if (this.IsVerbosityArg(argValues[0]))
                        {
                            UpdateVerbosity(argValues[1], newArgList);
                            continue;
                        }

                        if (Eq(argValues[0], _blame))
                        {
                            blame = true;
                        }

                        // Any blame-crash param implies that we collect crash dump
                        if (Eq(argValues[0], _blameCrash))
                        {
                            blame = true;
                            collectCrashDump = true;
                        }

                        if (Eq(argValues[0], _blameCrashCollectAlways))
                        {
                            blame = true;
                            collectCrashDump = true;
                            collectCrashDumpAlways = true;
                        }

                        if (Eq(argValues[0], _blameCrashDumpType))
                        {
                            blame = true;
                            collectCrashDump = true;
                            collectCrashDumpType = argValues[1];
                        }

                        // Any blame-hang param implies that we collect hang dump
                        if (Eq(argValues[0], _blameHang))
                        {
                            blame = true;
                            collectHangDump = true;
                        }

                        if (Eq(argValues[0], _blameHangDumpType))
                        {
                            blame = true;
                            collectHangDump = true;
                            collectHangDumpType = argValues[1];
                        }

                        if (Eq(argValues[0], _blameHangTimeout))
                        {
                            blame = true;
                            collectHangDump = true;
                            collectHangDumpTimeout = argValues[1];
                        }

                        // Check if the argument is shortname
                        if (ArgumentMapping.TryGetValue(argValues[0].ToLower(), out var longName))
                        {
                            argValues[0] = longName;
                        }

                        newArgList.Add(string.Join(":", argValues));
                    }
                    else
                    {
                        if (Eq(arg, _blame))
                        {
                            blame = true;
                        }
                        else if (Eq(arg, _blameCrash))
                        {
                            blame = true;
                            collectCrashDump = true;
                        }
                        else if (Eq(arg, _blameCrashCollectAlways))
                        {
                            blame = true;
                            collectCrashDump = true;
                            collectCrashDumpAlways = true;
                        }
                        else if (Eq(arg, _blameHang))
                        {
                            blame = true;
                            collectHangDump = true;
                        }
                        else
                        {
                            activeArgument = arg.ToLower();
                            if (ArgumentMapping.TryGetValue(activeArgument, out var value))
                            {
                                activeArgument = value;
                            }
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(activeArgument))
                {
                    if (IsVerbosityArg(activeArgument))
                    {
                        UpdateVerbosity(arg, newArgList);
                    }
                    else if (IgnoredArguments.Contains(activeArgument))
                    {
                        ignoredArgs.Add(activeArgument);
                        ignoredArgs.Add(arg);
                    }
                    else if (Eq(activeArgument, _blame))
                    {
                        blame = true;
                    }
                    else if (Eq(activeArgument, _blameCrash))
                    {
                        blame = true;
                        collectCrashDump = true;
                    }
                    else if (Eq(activeArgument, _blameCrashCollectAlways))
                    {
                        blame = true;
                        collectCrashDump = true;
                        collectCrashDumpAlways = true;
                    }
                    else if (Eq(activeArgument, _blameCrashDumpType))
                    {
                        blame = true;
                        collectCrashDump = true;
                        collectCrashDumpType = arg;
                    }
                    else if (Eq(activeArgument, _blameHang))
                    {
                        blame = true;
                        collectHangDump = true;
                    }
                    else if (Eq(activeArgument, _blameHangDumpType))
                    {
                        blame = true;
                        collectHangDump = true;
                        collectHangDumpType = arg;
                    }
                    else if (Eq(activeArgument, _blameHangTimeout))
                    {
                        blame = true;
                        collectHangDump = true;
                        collectHangDumpTimeout = arg;
                    }
                    else
                    {
                        newArgList.Add(string.Concat(activeArgument, ":", arg));
                    }

                    activeArgument = null;
                }
                else
                {
                    if (Eq(arg, _blame))
                    {
                        blame = true;
                    }
                    else if (Eq(arg, _blameCrash))
                    {
                        blame = true;
                        collectCrashDump = true;
                    }
                    else if (Eq(arg, _blameCrashCollectAlways))
                    {
                        blame = true;
                        collectCrashDump = true;
                        collectCrashDumpAlways = true;
                    }
                    else if (Eq(arg, _blameHang))
                    {
                        blame = true;
                        collectHangDump = true;
                    }
                    else
                    {
                        newArgList.Add(arg);
                    }
                }
            }

            if (!string.IsNullOrEmpty(activeArgument))
            {
                if (IgnoredArguments.Contains(activeArgument))
                {
                    ignoredArgs.Add(activeArgument);
                }
                else
                {
                    newArgList.Add(activeArgument);
                }
            }

            if (blame)
            {
                string crashDumpArgs = null;
                string hangDumpArgs = null;

                if (collectCrashDump)
                {
                    crashDumpArgs = "CollectDump";
                    if (collectCrashDumpAlways)
                    {
                        crashDumpArgs += ";CollectAlways=true";
                    }

                    if (!string.IsNullOrWhiteSpace(collectCrashDumpType))
                    {
                        crashDumpArgs += $";DumpType={collectCrashDumpType}";
                    }
                }

                if (collectHangDump)
                {
                    hangDumpArgs = "CollectHangDump";
                    if (!string.IsNullOrWhiteSpace(collectHangDumpType))
                    {
                        hangDumpArgs += $";DumpType={collectHangDumpType}";
                    }

                    if (!string.IsNullOrWhiteSpace(collectHangDumpTimeout))
                    {
                        hangDumpArgs += $";TestTimeout={collectHangDumpTimeout}";
                    }
                }

                if (collectCrashDump || collectHangDump)
                {
                    newArgList.Add($@"--blame:""{string.Join(";", crashDumpArgs, hangDumpArgs).Trim(';')}""");
                }
                else
                {
                    newArgList.Add("--blame");
                }
            }

            return newArgList;
        }

        private bool IsVerbosityArg(string arg)
        {
            return string.Equals(arg, "-v", System.StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "--verbosity", System.StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateVerbosity(string verbosity, List<string> newArgList)
        {
            if (VerbosityMapping.TryGetValue(verbosity.ToLower(), out string longValue))
            {
                newArgList.Add(verbosityString + longValue);
                return;
            }
            newArgList.Add(verbosityString + verbosity);
        }

        private bool Eq(string left, string right)
        {
            return string.Equals(left, right, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
