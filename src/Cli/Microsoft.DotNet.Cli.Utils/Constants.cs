// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Cli.Utils;

public static class Constants
{
    public const string DefaultConfiguration = "Debug";

    public static readonly string ProjectFileName = "project.json";
    public static readonly string ToolManifestFileName = "dotnet-tools.json";
    public static readonly string DotConfigDirectoryName = ".config";
    public static readonly string ExeSuffix =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;

    public static readonly string BinDirectoryName = "bin";
    public static readonly string ObjDirectoryName = "obj";
    public static readonly string GitDirectoryName = ".git";

    public static readonly string MSBUILD_EXE_PATH = "MSBUILD_EXE_PATH";
    public static readonly string MSBuildExtensionsPath = "MSBuildExtensionsPath";
    public static readonly string EnableDefaultItems = "EnableDefaultItems";

    public static readonly string ProjectArgumentName = "<PROJECT>";
    public static readonly string SolutionArgumentName = "<SLN_FILE>";
    public static readonly string ToolPackageArgumentName = "<PACKAGE_ID>";

    public static readonly string AnyRid = "any";

    public static readonly string RestoreInteractiveOption = "--interactive";
    public static readonly string workloadSetVersionFileName = "workloadVersion.txt";

    /// <summary>
    /// Adds performance optimizations to restore by disabling default item globbing
    /// if the user hasn't already specified EnableDefaultItems.
    /// </summary>
    public static IEnumerable<string> AddRestoreOptimizations(IEnumerable<string> msbuildArgs)
    {
        var args = msbuildArgs.ToList();
        
        // Check if user has already specified EnableDefaultItems
        bool userSpecifiedEnableDefaultItems = HasUserSpecifiedProperty(args, EnableDefaultItems);
        
        if (!userSpecifiedEnableDefaultItems)
        {
            // Add EnableDefaultItems=false to improve restore performance by disabling default item globbing
            args.Insert(0, $"-property:{EnableDefaultItems}=false");
        }
        
        return args;
    }

    /// <summary>
    /// Checks if the user has already specified a given property in the MSBuild arguments.
    /// </summary>
    private static bool HasUserSpecifiedProperty(IEnumerable<string> args, string propertyName)
    {
        foreach (var arg in args)
        {
            // Check for -property:PropertyName=, -p:PropertyName=, --property:PropertyName=, etc.
            if (arg.StartsWith("-property:", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-p:", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("--property:", StringComparison.OrdinalIgnoreCase))
            {
                // Extract the property part after the prefix
                var colonIndex = arg.IndexOf(':');
                if (colonIndex > 0 && colonIndex < arg.Length - 1)
                {
                    var propertyPart = arg.Substring(colonIndex + 1);
                    var equalsIndex = propertyPart.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        var propName = propertyPart.Substring(0, equalsIndex);
                        if (string.Equals(propName, propertyName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        
        return false;
    }
}
