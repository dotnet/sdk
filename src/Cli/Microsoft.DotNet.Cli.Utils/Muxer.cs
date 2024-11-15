// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Utils
{
    public class Muxer
    {
        public static readonly string MuxerName = "dotnet";

        private readonly string _muxerPath;

        internal string SharedFxVersion
        {
            get
            {
                var depsFile = new FileInfo(GetDataFromAppDomain("FX_DEPS_FILE"));
                return depsFile.Directory.Name;
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
#if NET6_0_OR_GREATER
            string processPath = Environment.ProcessPath;
            string hostName = "dotnet" + Constants.ExeSuffix;

            if (!processPath.EndsWith(hostName, StringComparison.OrdinalIgnoreCase))
            {
                var dotnetHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Environment.GetEnvironmentVariable("DOTNET_ROOT");
                if (!string.IsNullOrEmpty(dotnetHostPath))
                {
                    processPath = dotnetHostPath + Path.DirectorySeparatorChar + hostName;
                }
            }
            _muxerPath = processPath;
#else
            _muxerPath = Process.GetCurrentProcess().MainModule.FileName;
#endif
        }

        public static string GetDataFromAppDomain(string propertyName)
        {
            return AppContext.GetData(propertyName) as string;
        }
    }
}
