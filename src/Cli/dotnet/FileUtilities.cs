// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools
{
    internal static class FileUtilities
    {
        internal static string GetFullPath(string fileSpec, string currentDirectory)
        {
            // Sending data out of the engine into the filesystem, so time to unescape.
            fileSpec = FixFilePath(EscapingUtilities.UnescapeAll(fileSpec));

            // Data coming back from the filesystem into the engine, so time to escape it back.
            string fullPath = EscapingUtilities.Escape(NormalizePath(Path.Combine(currentDirectory, fileSpec)));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !EndsWithSlash(fullPath))
            {
                if (FileUtilitiesRegex.IsDrivePattern(fileSpec) ||
                    FileUtilitiesRegex.IsUncPattern(fullPath))
                {
                    // append trailing slash if Path.GetFullPath failed to (this happens with drive-specs and UNC shares)
                    fullPath += Path.DirectorySeparatorChar;
                }
            }

            return fullPath;
        }

        internal static string FixFilePath(string path)
        {
            return string.IsNullOrEmpty(path) || Path.DirectorySeparatorChar == '\\' ? path : path.Replace('\\', '/'); // .Replace("//", "/");
        }

        internal static string NormalizePath(string path)
        {
            //if (string.IsNullOrEmpty(path))
            //{
            //    throw ArgumentNullException("", (Exception?)null);
            //}

            string fullPath = GetFullPath(path);
            return FixFilePath(fullPath);
        }

        private static string GetFullPath(string path)
        {
#if FEATURE_LEGACY_GETFULLPATH
            if (NativeMethodsShared.IsWindows)
            {
                string uncheckedFullPath = NativeMethodsShared.GetFullPath(path);

                if (IsPathTooLong(uncheckedFullPath))
                {
                    string message = ResourceUtilities.FormatString(AssemblyResources.GetString("Shared.PathTooLong"), path, NativeMethodsShared.MaxPath);
                    throw new PathTooLongException(message);
                }

                // We really don't care about extensions here, but Path.HasExtension provides a great way to
                // invoke the CLR's invalid path checks (these are independent of path length)
                Path.HasExtension(uncheckedFullPath);

                // If we detect we are a UNC path then we need to use the regular get full path in order to do the correct checks for UNC formatting
                // and security checks for strings like \\?\GlobalRoot
                return IsUNCPath(uncheckedFullPath) ? Path.GetFullPath(uncheckedFullPath) : uncheckedFullPath;
            }
#endif
            return Path.GetFullPath(path);
        }

        internal static bool EndsWithSlash(string fileSpec)
        {
            return (fileSpec.Length > 0)
                ? IsSlash(fileSpec[fileSpec.Length - 1])
                : false;
        }

        internal static bool IsSlash(char c)
        {
            return (c == Path.DirectorySeparatorChar) || (c == Path.AltDirectorySeparatorChar);
        }
    }
}
