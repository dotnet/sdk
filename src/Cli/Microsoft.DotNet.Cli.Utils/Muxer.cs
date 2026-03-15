// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET6_0_OR_GREATER
using System.Diagnostics;
#endif

namespace Microsoft.DotNet.Cli.Utils;

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
        string muxerFileName = MuxerName + Constants.ExeSuffix;

        // Check environment variables first to allow package managers and
        // other tools to explicitly set the dotnet root.  This is needed
        // when the SDK is installed via symlinks (e.g. Nix, Guix) where
        // the directory-traversal heuristic below would resolve symlinks
        // and find the wrong root directory.
        string? dotnetHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (dotnetHostPath is not null && File.Exists(dotnetHostPath))
        {
            _muxerPath = dotnetHostPath;
        }

        if (_muxerPath is null)
        {
            var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (dotnetRoot is not null)
            {
                string rootMuxer = Path.Combine(dotnetRoot, muxerFileName);
                if (File.Exists(rootMuxer))
                {
                    _muxerPath = rootMuxer;
                }
            }
        }

        if (_muxerPath is null)
        {
            // Most scenarios are running dotnet.dll as the app
            // Root directory with muxer should be two above app base: <root>/sdk/<version>
            string? rootPath = Path.GetDirectoryName(Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)));
            if (rootPath is not null)
            {
                string muxerPathMaybe = Path.Combine(rootPath, muxerFileName);
                if (File.Exists(muxerPathMaybe))
                {
                    _muxerPath = muxerPathMaybe;
                }
            }
        }

        if (_muxerPath is null)
        {
            // Last resort: if the current process is the dotnet muxer itself, use its path.
#if NET6_0_OR_GREATER
            string? processPath = Environment.ProcessPath;
#else
            string processPath = Process.GetCurrentProcess().MainModule.FileName;
#endif

            if (processPath is not null && Path.GetFileName(processPath).Equals(muxerFileName, StringComparison.OrdinalIgnoreCase))
            {
                _muxerPath = processPath;
            }
        }
    }

    public static string? GetDataFromAppDomain(string propertyName)
    {
        return AppContext.GetData(propertyName) as string;
    }
}
