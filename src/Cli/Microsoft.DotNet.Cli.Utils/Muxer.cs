// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils;

public class Muxer
{
    public static readonly string MuxerName = "dotnet";

    private readonly IPathResolver _pathResolver;

    internal string SharedFxVersion
    {
        get
        {
            var depsFile = new FileInfo(GetDataFromAppDomain("FX_DEPS_FILE") ?? string.Empty);
            return depsFile.Directory?.Name ?? string.Empty;
        }
    }

    public string MuxerPath => _pathResolver.DotnetExecutable;

    public Muxer(IPathResolver? pathResolver = null)
    {
        _pathResolver = pathResolver ?? PathResolver.Default;
    }

    public static string? GetDataFromAppDomain(string propertyName)
    {
        return AppContext.GetData(propertyName) as string;
    }
}
