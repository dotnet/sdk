// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable enable
#pragma warning restore IDE0240 // Remove redundant nullable directive
namespace Microsoft.DotNet.NativeWrapper
{
    public class EnvironmentProvider
    {
        private static readonly char[] s_invalidPathChars = Path.GetInvalidPathChars();

        private IEnumerable<string>? _searchPaths;

        private readonly Func<string, string?> _getEnvironmentVariable;
        private readonly Func<string?> _getCurrentProcessPath;

        public EnvironmentProvider(Func<string, string?> getEnvironmentVariable)
            : this(getEnvironmentVariable, GetCurrentProcessPath)
        { }

        public EnvironmentProvider(Func<string, string?> getEnvironmentVariable, Func<string?> getCurrentProcessPath)
        {
            _getEnvironmentVariable = getEnvironmentVariable;
            _getCurrentProcessPath = getCurrentProcessPath;
        }

        private IEnumerable<string> SearchPaths
        {
            get
            {
                _searchPaths ??=
                    _getEnvironmentVariable(Constants.PATH)!
                    .Split(new char[] { Path.PathSeparator }, options: StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim('"'))
                    .Where(p => p.IndexOfAny(s_invalidPathChars) == -1)
                    .ToList();

                return _searchPaths;
            }
        }

        public string? GetCommandPath(string commandName)
        {
            var commandNameWithExtension = commandName + Constants.ExeSuffix;
            var commandPath = SearchPaths
                .Select(p => Path.Combine(p, commandNameWithExtension))
                .FirstOrDefault(File.Exists);

            return commandPath;
        }

        public string? GetDotnetExeDirectory(Action<FormattableString>? log = null)
        {
            string? environmentOverride = _getEnvironmentVariable(Constants.DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR);
            if (!string.IsNullOrEmpty(environmentOverride))
            {
                log?.Invoke($"GetDotnetExeDirectory: {Constants.DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR} set to {environmentOverride}");
                return environmentOverride;
            }

            string? dotnetExe;
#if NETCOREAPP
            // The dotnet executable is loading only the .NET version of this code so there is no point checking
            // the current process path on .NET Framework. We are expected to find dotnet on PATH.
            dotnetExe = _getCurrentProcessPath();

            if (string.IsNullOrEmpty(dotnetExe) || !Path.GetFileNameWithoutExtension(dotnetExe)
                    .Equals(Constants.DotNet, StringComparison.InvariantCultureIgnoreCase))
#endif
            {
                string? dotnetExeFromPath = GetCommandPath(Constants.DotNet);

                if (dotnetExeFromPath != null && !Interop.RunningOnWindows)
                {
                    // e.g. on Linux the 'dotnet' command from PATH is a symlink so we need to
                    // resolve it to get the actual path to the binary
                    dotnetExeFromPath = Interop.Unix.realpath(dotnetExeFromPath) ?? dotnetExeFromPath;
                }

                if (!string.IsNullOrWhiteSpace(dotnetExeFromPath))
                {
                    dotnetExe = dotnetExeFromPath;
                }
                else
                {
                    log?.Invoke($"GetDotnetExeDirectory: dotnet command path not found.  Using current process");
                    log?.Invoke($"GetDotnetExeDirectory: Path variable: {_getEnvironmentVariable(Constants.PATH)}");

#if !NETCOREAPP
                    // If we failed to find dotnet on PATH, we revert to the old behavior of returning the current process
                    // path. This is really an error state but we're keeping the contract of always returning a non-empty
                    // path for backward compatibility.
                    dotnetExe = _getCurrentProcessPath();
#endif
                }
            }

            var dotnetDirectory = Path.GetDirectoryName(dotnetExe);

            log?.Invoke($"GetDotnetExeDirectory: Returning {dotnetDirectory}");

            return dotnetDirectory;
        }

        public static string? GetDotnetExeDirectory(Func<string, string?>? getEnvironmentVariable = null, Action<FormattableString>? log = null)
        {
            if (getEnvironmentVariable == null)
            {
                getEnvironmentVariable = Environment.GetEnvironmentVariable;
            }
            var environmentProvider = new EnvironmentProvider(getEnvironmentVariable);
            return environmentProvider.GetDotnetExeDirectory(log);
        }

        public static string? GetDotnetExeDirectory(Func<string, string?> getEnvironmentVariable, Func<string?>? getCurrentProcessPath, Action<FormattableString>? log = null)
        {
            getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
            getCurrentProcessPath ??= GetCurrentProcessPath;
            var environmentProvider = new EnvironmentProvider(getEnvironmentVariable, getCurrentProcessPath);
            return environmentProvider.GetDotnetExeDirectory();
        }

        private static string? GetCurrentProcessPath()
        {
            string? currentProcessPath;
#if NET6_0_OR_GREATER
            currentProcessPath = Environment.ProcessPath;
#else
            currentProcessPath = Process.GetCurrentProcess().MainModule.FileName;
#endif
            return currentProcessPath;
        }
    }
}
