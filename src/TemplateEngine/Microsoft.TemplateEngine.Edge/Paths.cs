using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge
{
    public static class Paths
    {
        public static string ProcessPath(this string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (path[0] != '~')
            {
                return path;
            }

            return Path.Combine(EngineEnvironmentSettings.Paths.UserProfileDir, path.Substring(1));
        }

        public static void Copy(this string path, string targetPath)
        {
            if (EngineEnvironmentSettings.Host.FileSystem.FileExists(path))
            {
                EngineEnvironmentSettings.Host.FileSystem.FileCopy(path, targetPath, true);
                return;
            }

            foreach (string p in path.EnumerateFiles("*", SearchOption.AllDirectories).OrderBy(x => x.Length))
            {
                string localPath = p.Substring(path.Length).TrimStart('\\', '/');

                if (EngineEnvironmentSettings.Host.FileSystem.DirectoryExists(p))
                {
                    localPath.CreateDirectory(targetPath);
                }
                else
                {
                    int parentDirEndIndex = localPath.LastIndexOfAny(new[] { '/', '\\' });
                    string wholeTargetPath = Path.Combine(targetPath, localPath);

                    if (parentDirEndIndex > -1)
                    {
                        string parentDir = localPath.Substring(0, parentDirEndIndex);
                        parentDir.CreateDirectory(targetPath);
                    }

                    EngineEnvironmentSettings.Host.FileSystem.FileCopy(p, wholeTargetPath, true);
                }
            }
        }

        public static void CreateDirectory(this string path, string parent)
        {
            string[] parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            string current = parent;

            for (int i = 0; i < parts.Length; ++i)
            {
                current = Path.Combine(current, parts[i]);
                EngineEnvironmentSettings.Host.FileSystem.CreateDirectory(current);
            }
        }

        public static void CreateDirectory(this string path)
        {
            EngineEnvironmentSettings.Host.FileSystem.CreateDirectory(path);
        }

        public static void Delete(this string path)
        {
            path.DeleteDirectory();
            path.DeleteFile();
        }

        public static void Delete(this string path, params string[] patterns)
        {
            path.Delete(SearchOption.TopDirectoryOnly, patterns);
        }

        public static void Delete(this string path, SearchOption searchOption, params string[] patterns)
        {
            if (!path.DirectoryExists())
            {
                return;
            }

            foreach (string pattern in patterns)
            {
                foreach (string entry in path.EnumerateFileSystemEntries(pattern, searchOption).ToList())
                {
                    entry.Delete();
                }
            }
        }

        public static void DeleteDirectory(this string path)
        {
            if (EngineEnvironmentSettings.Host.FileSystem.DirectoryExists(path))
            {
                EngineEnvironmentSettings.Host.FileSystem.DirectoryDelete(path, true);
            }
        }

        public static void DeleteFile(this string path)
        {
            if (EngineEnvironmentSettings.Host.FileSystem.FileExists(path))
            {
                EngineEnvironmentSettings.Host.FileSystem.FileDelete(path);
            }
        }

        public static bool DirectoryExists(this string path)
        {
            return EngineEnvironmentSettings.Host.FileSystem.DirectoryExists(path);
        }

        public static IEnumerable<string> EnumerateDirectories(this string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (EngineEnvironmentSettings.Host.FileSystem.DirectoryExists(path))
            {
                return EngineEnvironmentSettings.Host.FileSystem.EnumerateDirectories(path, pattern, searchOption);
            }

            return Enumerable.Empty<string>();
        }

        public static IEnumerable<string> EnumerateFiles(this string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (EngineEnvironmentSettings.Host.FileSystem.FileExists(path))
            {
                return new[] { path };
            }

            if (EngineEnvironmentSettings.Host.FileSystem.DirectoryExists(path))
            {
                return EngineEnvironmentSettings.Host.FileSystem.EnumerateFiles(path.ProcessPath(), pattern, searchOption);
            }

            return Enumerable.Empty<string>();
        }

        public static IEnumerable<string> EnumerateFileSystemEntries(this string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (EngineEnvironmentSettings.Host.FileSystem.FileExists(path))
            {
                return new[] { path };
            }

            if (EngineEnvironmentSettings.Host.FileSystem.DirectoryExists(path))
            {
                return EngineEnvironmentSettings.Host.FileSystem.EnumerateFileSystemEntries(path.ProcessPath(), pattern, searchOption);
            }

            return Enumerable.Empty<string>();
        }

        public static bool Exists(this string path)
        {
            return path.FileExists() || path.DirectoryExists();
        }

        public static bool FileExists(this string path)
        {
            return EngineEnvironmentSettings.Host.FileSystem.FileExists(path);
        }

        public static string Name(this string path)
        {
            return Path.GetFileName(path);
        }

        public static string ReadAllText(this string path, string defaultValue = "")
        {
            return path.Exists() ? EngineEnvironmentSettings.Host.FileSystem.ReadAllText(path) : defaultValue;
        }

        public static string ToPath(this string codebase)
        {
            Uri cb = new Uri(codebase, UriKind.Absolute);
            string localPath = cb.LocalPath;
            return localPath;
        }

        public static void WriteAllText(this string path, string value)
        {
            string parentDir = Path.GetDirectoryName(path);

            if (!parentDir.Exists())
            {
                EngineEnvironmentSettings.Host.FileSystem.CreateDirectory(parentDir);
            }

            EngineEnvironmentSettings.Host.FileSystem.WriteAllText(path, value);
        }

        private static string GetOrComputePath(ref string cache, params string[] paths)
        {
            return cache ?? (cache = Path.Combine(paths));
        }

        public static class Global
        {
            private static string _baseDir;
            private static string _builtInsFeed;
            private static string _defaultInstallPackageList;
            private static string _defaultInstallTemplateList;

            public static string BaseDir
            {
                get
                {
                    if (_baseDir == null)
                    {
                        Assembly asm = Assembly.GetEntryAssembly();
                        Uri codebase = new Uri(asm.CodeBase, UriKind.Absolute);
                        string localPath = codebase.LocalPath;
                        _baseDir = Path.GetDirectoryName(localPath);
                    }

                    return _baseDir;
                }
            }

            public static string BuiltInsFeed => GetOrComputePath(ref _builtInsFeed, BaseDir, "BuiltIns");

            public static string DefaultInstallPackageList => GetOrComputePath(ref _defaultInstallPackageList, BaseDir, "defaultinstall.package.list");

            public static string DefaultInstallTemplateList => GetOrComputePath(ref _defaultInstallTemplateList, BaseDir, "defaultinstall.template.list");
        }

        public static class User
        {
            private static string _aliasesFile;
            private static string _firstRunCookie;
            private static string _nuGetConfig;
            private static string _packageCache;
            private static string _scratchDir;
            private static string _settingsFile;
            private static string _contentDir;

            public static string AliasesFile => GetOrComputePath(ref _aliasesFile, BaseDir, "aliases.json");

            public static string BaseDir => EngineEnvironmentSettings.Paths.BaseDir;

            public static string Content => GetOrComputePath(ref _contentDir, BaseDir, "content");

            public static string FirstRunCookie => GetOrComputePath(ref _firstRunCookie, BaseDir, ".firstrun");

            public static string PackageCache => GetOrComputePath(ref _packageCache, EngineEnvironmentSettings.Paths.UserProfileDir, ".nuget", "packages");

            public static string ScratchDir => GetOrComputePath(ref _scratchDir, BaseDir, "scratch");

            public static string SettingsFile => GetOrComputePath(ref _settingsFile, BaseDir, "settings.json");

            public static string CultureNeutralTemplateCacheFile
            {
                get
                {
                    return ExplicitLocaleTemplateCacheFile(null);
                }
            }

            public static string CurrentLocaleTemplateCacheFile
            {
                get
                {
                    return ExplicitLocaleTemplateCacheFile(EngineEnvironmentSettings.Host.Locale);
                }
            }

            public static readonly string TemplateCacheFileBaseName = "templatecache.json";

            public static string ExplicitLocaleTemplateCacheFile(string locale)
            {
                string filename;

                if (string.IsNullOrEmpty(locale))
                {
                    filename = TemplateCacheFileBaseName;
                }
                else
                {
                    filename = locale + "." + TemplateCacheFileBaseName;
                }

                string tempCache = null;    // don't cache, the locale could change
                return GetOrComputePath(ref tempCache, BaseDir, filename);
            }

            public static string NuGetConfig => GetOrComputePath(ref _nuGetConfig, BaseDir, "NuGet.config");
        }
    }
}
