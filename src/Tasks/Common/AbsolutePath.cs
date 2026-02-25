// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This is a polyfill for the AbsolutePath struct from MSBuild.
// See: https://github.com/dotnet/msbuild/blob/main/src/Framework/PathHelpers/AbsolutePath.cs

#if NETFRAMEWORK

#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Represents an absolute file system path.
    /// </summary>
    public readonly struct AbsolutePath : IEquatable<AbsolutePath>
    {
        private static readonly bool s_isFileSystemCaseSensitive = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                                                   && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        private static readonly StringComparer s_pathComparer = s_isFileSystemCaseSensitive
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;

        /// <summary>
        /// The normalized string representation of this path.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// The original string used to create this path.
        /// </summary>
        public string OriginalValue { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbsolutePath"/> struct.
        /// </summary>
        public AbsolutePath(string path)
        {
            ValidatePath(path);
            Value = path;
            OriginalValue = path;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbsolutePath"/> struct.
        /// </summary>
        internal AbsolutePath(string path, bool ignoreRootedCheck)
            : this(path, path, ignoreRootedCheck)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbsolutePath"/> struct.
        /// </summary>
        internal AbsolutePath(string path, string original, bool ignoreRootedCheck)
        {
            if (!ignoreRootedCheck)
            {
                ValidatePath(path);
            }
            Value = path;
            OriginalValue = original;
        }

        private static void ValidatePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path must not be null or empty.", nameof(path));
            }

            if (!IsPathFullyQualified(path))
            {
                throw new ArgumentException("Path must be rooted.", nameof(path));
            }
        }

        private static bool IsPathFullyQualified(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                return false;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On non-Windows, a rooted path is fully qualified.
                return true;
            }

            // Windows: drive-rooted paths like "C:\foo" are fully qualified.
            if (path.Length >= 3 && path[1] == ':')
            {
                char separator = path[2];
                return separator == Path.DirectorySeparatorChar || separator == Path.AltDirectorySeparatorChar;
            }

            // UNC/extended paths like "\\server\share" or "\\?\C:\foo" are fully qualified.
            if (path.Length >= 2 && IsDirectorySeparator(path[0]) && IsDirectorySeparator(path[1]))
            {
                return true;
            }

            // Rooted with single leading separator (e.g., "\foo") is drive-relative on Windows.
            return false;
        }

        private static bool IsDirectorySeparator(char c)
        {
            return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
        }

        /// <summary>
        /// Initializes a new instance by combining an absolute base path with a relative path.
        /// </summary>
        public AbsolutePath(string path, AbsolutePath basePath)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path must not be null or empty.", nameof(path));
            }

            Value = Path.Combine(basePath.Value, path);
            OriginalValue = path;
        }

        /// <summary>
        /// Implicitly converts an AbsolutePath to a string.
        /// </summary>
        public static implicit operator string(AbsolutePath path) => path.Value;

        /// <summary>
        /// Returns the canonical form of this path.
        /// </summary>
        internal AbsolutePath GetCanonicalForm()
        {
            if (string.IsNullOrEmpty(Value))
            {
                return this;
            }

            bool hasRelativeSegment = Value.Contains("/.") || Value.Contains("\\.");
            bool needsSeparatorNormalization = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                              && Value.IndexOf(Path.AltDirectorySeparatorChar) >= 0;

            if (!hasRelativeSegment && !needsSeparatorNormalization)
            {
                return this;
            }

            return new AbsolutePath(Path.GetFullPath(Value), OriginalValue, ignoreRootedCheck: true);
        }

        public static bool operator ==(AbsolutePath left, AbsolutePath right) => left.Equals(right);
        public static bool operator !=(AbsolutePath left, AbsolutePath right) => !left.Equals(right);

        public override bool Equals(object? obj) => obj is AbsolutePath other && Equals(other);

        public bool Equals(AbsolutePath other) => s_pathComparer.Equals(Value, other.Value);

        public override int GetHashCode() => Value is null ? 0 : s_pathComparer.GetHashCode(Value);

        public override string ToString() => Value;
    }
}

#endif
