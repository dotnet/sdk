// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET6_0_OR_GREATER
using System.Diagnostics;
#endif

namespace Microsoft.DotNet.Cli.Utils
{
    public class Muxer
    {
        public static readonly string MuxerName = "dotnet";

        private readonly string? _muxerPath;

        internal string SharedFxVersion
        {
            get
            {
                var depsFile = new FileInfo(GetDataFromAppDomain("FX_DEPS_FILE") ?? string.Empty);
                return depsFile.Directory?.Name ?? string.Empty;
            }
        }

        public string MuxerPath
        {
            get
            {
                if (_muxerPath == null)
                {
                    throw new InvalidOperationException(LocalizableStrings.UnableToLocateDotnetMultiplexer);
                }
                return _muxerPath;
            }
        }

        public Muxer()
        {
            // Most scenarios are running dotnet.dll as the app
            // Root directory with muxer should be two above app base: <root>/sdk/<version>
            string? rootPath = Path.GetDirectoryName(Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)));
            if (rootPath is not null)
            {
                string muxerPathMaybe = Path.Combine(rootPath, $"{MuxerName}{FileNameSuffixes.CurrentPlatform.Exe}");
                if (File.Exists(muxerPathMaybe))
                {
                    _muxerPath = muxerPathMaybe;
                }
            }

            if (_muxerPath is null)
            {
                // Best-effort search for muxer.
                // SDK sets DOTNET_HOST_PATH as absolute path to current dotnet executable
#if NET6_0_OR_GREATER
                string? processPath = Environment.ProcessPath;
#else
                string processPath = Process.GetCurrentProcess().MainModule.FileName;
#endif

                // The current process should be dotnet in most normal scenarios except when dotnet.dll is loaded in a custom host like the testhost
                if (processPath is not null && !Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                {
                    // SDK sets DOTNET_HOST_PATH as absolute path to current dotnet executable
                    processPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
                    if (processPath is null)
                    {
                        // fallback to DOTNET_ROOT which typically holds some dotnet executable
                        var root = Environment.GetEnvironmentVariable("DOTNET_ROOT");
                        if (root is not null)
                        {
                            processPath = Path.Combine(root, $"dotnet{Constants.ExeSuffix}");
                        }
                    }
                }

                _muxerPath = processPath;
            }
        }

        public static string? GetDataFromAppDomain(string propertyName)
        {
            return AppContext.GetData(propertyName) as string;
        }
    }
}
