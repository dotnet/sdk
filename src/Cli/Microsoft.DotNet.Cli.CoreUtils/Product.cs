// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System.Reflection;

namespace Microsoft.DotNet.Cli.Utils;

public static class Product
{
    public static readonly string Version;
    public static readonly string TargetFrameworkVersion = "11.0";

    static Product()
    {
        DotnetVersionFile versionFile = DotnetFiles.VersionFileObject;
        Version = versionFile.BuildNumber
            ?? typeof(Product).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
            ?? string.Empty;
    }
}

#endif
