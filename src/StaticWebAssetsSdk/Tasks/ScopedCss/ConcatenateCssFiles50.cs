// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET9_0_OR_GREATER
using System.Globalization;
#endif
using System.Security.Cryptography;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ConcatenateCssFiles50 : Task
{
    private static readonly char[] _separator = ['/'];

    private static readonly IComparer<ITaskItem> _fullPathComparer =
        Comparer<ITaskItem>.Create((x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.GetMetadata("FullPath"), y.GetMetadata("FullPath")));

    [Required]
    public ITaskItem[] ScopedCssFiles { get; set; }

    [Required]
    public ITaskItem[] ProjectBundles { get; set; }

    [Required]
    public string ScopedCssBundleBasePath { get; set; }

    [Required]
    public string OutputFile { get; set; }

    public override bool Execute()
    {
        if (ProjectBundles.Length > 0)
        {
            Array.Sort(ProjectBundles, _fullPathComparer);
        }
        Array.Sort(ScopedCssFiles, _fullPathComparer);

        var builder = new StringBuilder();
        if (ProjectBundles.Length > 0)
        {
            // We are importing bundles from other class libraries and packages, in that case we need to compute the
            // import path relative to the position of where the final bundle will be.
            // Our final bundle will always be at "<<CurrentBasePath>>/scoped.styles.css"
            // Other bundles will be at "<<BundleBasePath>>/bundle.bdl.scp.css"
            // The base and relative paths can be modified by the user, so we do a normalization process to ensure they
            // are in the shape we expect them before we use them.
            // We normalize path separators to '\' from '/' which is what we expect on a url. The separator can come as
            // '\' as a result of user input or another MSBuild path normalization operation. We always want '/' since that
            // is what is valid on the url.
            // We remove leading and trailing '/' on all paths to ensure we can combine them properly. Users might specify their
            // base path with or without forward and trailing slashes and we always need to make sure we combine them appropriately.
            // These links need to be relative to the final bundle to be independent of the path where the main app is being served.
            // For example:
            // An app is served from the "subdir" path base, the main bundle path on disk is "MyApp/scoped.styles.css" and it uses a
            // library with scoped components that is placed on "_content/library/bundle.bdl.scp.css".
            // The resulting import would be "import '../_content/library/bundle.bdl.scp.css'".
            // If we were to produce "/_content/library/bundle.bdl.scp.css" it would fail to accoutn for "subdir"
            // We could produce shorter paths if we detected common segments between the final bundle base path and the imported bundle
            // base paths, but its more work and it will not have a significant impact on the bundle size size.
            var normalizedBasePath = ConcatenateCssFiles50.NormalizePath(ScopedCssBundleBasePath);
            var currentBasePathSegments = normalizedBasePath.Split(_separator, StringSplitOptions.RemoveEmptyEntries);
            var prefix = string.Join("/", Enumerable.Repeat("..", currentBasePathSegments.Length));
            for (var i = 0; i < ProjectBundles.Length; i++)
            {
                var bundle = ProjectBundles[i];
                var bundleBasePath = ConcatenateCssFiles50.NormalizePath(bundle.GetMetadata("BasePath"));
                var relativePath = ConcatenateCssFiles50.NormalizePath(bundle.GetMetadata("RelativePath"));
                var importPath = ConcatenateCssFiles50.NormalizePath(Path.Combine(prefix, bundleBasePath, relativePath));

#if !NET9_0_OR_GREATER
                builder.AppendLine($"@import '{importPath}';");
#else
                builder.AppendLine(CultureInfo.InvariantCulture, $"@import '{importPath}';");
#endif
            }

            builder.AppendLine();
        }

        for (var i = 0; i < ScopedCssFiles.Length; i++)
        {
            var current = ScopedCssFiles[i];
#if !NET9_0_OR_GREATER
            builder.AppendLine($"/* {ConcatenateCssFiles50.NormalizePath(current.GetMetadata("BasePath"))}/{ConcatenateCssFiles50.NormalizePath(current.GetMetadata("RelativePath"))} */");
#else
            builder.AppendLine(CultureInfo.InvariantCulture, $"/* {NormalizePath(current.GetMetadata("BasePath"))}/{NormalizePath(current.GetMetadata("RelativePath"))} */");
#endif
            foreach (var line in File.ReadLines(current.GetMetadata("FullPath")))
            {
                builder.AppendLine(line);
            }
        }

        var content = builder.ToString();

        if (!File.Exists(OutputFile) || !ConcatenateCssFiles50.SameContent(content, OutputFile))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputFile));
            File.WriteAllText(OutputFile, content);
        }

        return !Log.HasLoggedErrors;
    }

    private static string NormalizePath(string path) => path.Replace("\\", "/").Trim('/');

    private static bool SameContent(string content, string outputFilePath)
    {
        var contentHash = GetContentHash(content);

        var outputContent = File.ReadAllText(outputFilePath);
        var outputContentHash = GetContentHash(outputContent);

        for (var i = 0; i < outputContentHash.Length; i++)
        {
            if (outputContentHash[i] != contentHash[i])
            {
                return false;
            }
        }

        return true;

        static byte[] GetContentHash(string content)
        {
#if NET472_OR_GREATER
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
#else
            return SHA256.HashData(Encoding.UTF8.GetBytes(content));
#endif
        }
    }
}
