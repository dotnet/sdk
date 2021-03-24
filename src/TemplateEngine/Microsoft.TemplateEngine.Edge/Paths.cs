using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge
{
    public class Paths
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;

        public GlobalPaths Global { get; }

        public UserPaths User { get; }

        public Paths(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
            Global = new GlobalPaths(this);
            User = new UserPaths(this);
        }

        public string ProcessPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (path[0] != '~')
            {
                return path;
            }

            return Path.Combine(_environmentSettings.Paths.UserProfileDir, path.Substring(1));
        }

        public void Copy(string path, string targetPath)
        {
            if (_environmentSettings.Host.FileSystem.FileExists(path))
            {
                _environmentSettings.Host.FileSystem.FileCopy(path, targetPath, true);
                return;
            }

            foreach (string p in EnumerateFiles(path, "*", SearchOption.AllDirectories).OrderBy(x => x.Length))
            {
                string localPath = p.Substring(path.Length).TrimStart('\\', '/');

                if (_environmentSettings.Host.FileSystem.DirectoryExists(p))
                {
                    CreateDirectory(localPath, targetPath);
                }
                else
                {
                    int parentDirEndIndex = localPath.LastIndexOfAny(new[] { '/', '\\' });
                    string wholeTargetPath = Path.Combine(targetPath, localPath);

                    if (parentDirEndIndex > -1)
                    {
                        string parentDir = localPath.Substring(0, parentDirEndIndex);
                        CreateDirectory(parentDir, targetPath);
                    }

                    _environmentSettings.Host.FileSystem.FileCopy(p, wholeTargetPath, true);
                }
            }
        }

        public Stream OpenRead(string path)
        {
            return _environmentSettings.Host.FileSystem.OpenRead(path);
        }

        public byte[] ReadAllBytes(string path)
        {
            if (Exists(path))
            {
                using (Stream s = _environmentSettings.Host.FileSystem.OpenRead(path))
                {
                    byte[] buffer = new byte[s.Length];
                    s.Read(buffer, 0, buffer.Length);
                    return buffer;
                }
            }
            
            return null;
        }

        public void CreateDirectory(string path, string parent)
        {
            string[] parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            string current = parent;

            for (int i = 0; i < parts.Length; ++i)
            {
                current = Path.Combine(current, parts[i]);
                _environmentSettings.Host.FileSystem.CreateDirectory(current);
            }
        }

        public void CreateDirectory(string path)
        {
            _environmentSettings.Host.FileSystem.CreateDirectory(path);
        }

        public void Delete(string path)
        {
            DeleteDirectory(path);
            DeleteFile(path);
        }

        public void Delete(string path, params string[] patterns)
        {
            Delete(path, SearchOption.TopDirectoryOnly, patterns);
        }

        public void Delete(string path, SearchOption searchOption, params string[] patterns)
        {
            if (!DirectoryExists(path))
            {
                return;
            }

            foreach (string pattern in patterns)
            {
                foreach (string entry in EnumerateFileSystemEntries(path, pattern, searchOption).ToList())
                {
                    Delete(entry);
                }
            }
        }

        public void DeleteDirectory(string path)
        {
            if (_environmentSettings.Host.FileSystem.DirectoryExists(path))
            {
                _environmentSettings.Host.FileSystem.DirectoryDelete(path, true);
            }
        }

        public void DeleteFile(string path)
        {
            if (_environmentSettings.Host.FileSystem.FileExists(path))
            {
                _environmentSettings.Host.FileSystem.FileDelete(path);
            }
        }

        public bool DirectoryExists(string path)
        {
            return _environmentSettings.Host.FileSystem.DirectoryExists(path);
        }

        public IEnumerable<string> EnumerateDirectories(string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (_environmentSettings.Host.FileSystem.DirectoryExists(path))
            {
                return _environmentSettings.Host.FileSystem.EnumerateDirectories(path, pattern, searchOption);
            }

            return Enumerable.Empty<string>();
        }

        public IEnumerable<string> EnumerateFiles(string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (_environmentSettings.Host.FileSystem.FileExists(path))
            {
                return new[] { path };
            }

            if (_environmentSettings.Host.FileSystem.DirectoryExists(path))
            {
                return _environmentSettings.Host.FileSystem.EnumerateFiles(ProcessPath(path), pattern, searchOption);
            }

            return Enumerable.Empty<string>();
        }

        public IEnumerable<string> EnumerateFileSystemEntries(string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (_environmentSettings.Host.FileSystem.FileExists(path))
            {
                return new[] { path };
            }

            if (_environmentSettings.Host.FileSystem.DirectoryExists(path))
            {
                return _environmentSettings.Host.FileSystem.EnumerateFileSystemEntries(ProcessPath(path), pattern, searchOption);
            }

            return Enumerable.Empty<string>();
        }

        public bool Exists(string path)
        {
            return FileExists(path) || DirectoryExists(path);
        }

        public bool FileExists(string path)
        {
            return _environmentSettings.Host.FileSystem.FileExists(path);
        }

        public string Name(string path)
        {
            path = path.TrimEnd('/', '\\');
            return Path.GetFileName(path);
        }

        public string ReadAllText(string path, string defaultValue = "")
        {
            return Exists(path) ? _environmentSettings.Host.FileSystem.ReadAllText(path) : defaultValue;
        }

        public string ToPath(string codebase)
        {
            Uri cb = new Uri(codebase, UriKind.Absolute);
            string localPath = cb.LocalPath;
            return localPath;
        }

        public void WriteAllText(string path, string value)
        {
            string parentDir = Path.GetDirectoryName(path);

            if (!Exists(parentDir))
            {
                _environmentSettings.Host.FileSystem.CreateDirectory(parentDir);
            }

            _environmentSettings.Host.FileSystem.WriteAllText(path, value);
        }

        private string GetOrComputePath(ref string cache, params string[] paths)
        {
            return cache ?? (cache = Path.Combine(paths));
        }

        public class GlobalPaths
        {
            private string _baseDir;
            private string _builtInsFeed;
            private string _defaultInstallPackageList;
            private string _defaultInstallTemplateList;
            private readonly Paths _parent;

            public GlobalPaths(Paths parent)
            {
                _parent = parent;
            }

            public string BaseDir
            {
                get
                {
                    if (_baseDir == null)
                    {
                        Assembly asm = typeof(Paths).GetTypeInfo().Assembly;
                        Uri codebase = new Uri(asm.Location, UriKind.Absolute);
                        string localPath = codebase.LocalPath;
                        _baseDir = Path.GetDirectoryName(localPath);
                    }

                    return _baseDir;
                }
            }

            public string BuiltInsFeed => _parent.GetOrComputePath(ref _builtInsFeed, BaseDir, "BuiltIns");

            public string DefaultInstallPackageList => _parent.GetOrComputePath(ref _defaultInstallPackageList, BaseDir, "defaultinstall.package.list");

            public string DefaultInstallTemplateList => _parent.GetOrComputePath(ref _defaultInstallTemplateList, BaseDir, "defaultinstall.template.list");
        }

        public class UserPaths
        {
            private string _aliasesFile;
            private string _firstRunCookie;
            private string _nuGetConfig;
            private string _packageCache;
            private string _scratchDir;
            private string _settingsFile;
            private string _globalSettingsFile;
            private string _contentDir;
            private string _packagesDir;

            public UserPaths(Paths parent)
            {
                _parent = parent;
            }

            public string AliasesFile => _parent.GetOrComputePath(ref _aliasesFile, BaseDir, "aliases.json");

            public string BaseDir => _parent._environmentSettings.Paths.BaseDir;

            public string Content => _parent.GetOrComputePath(ref _contentDir, BaseDir, "content");

            public string Packages => _parent.GetOrComputePath(ref _packagesDir, BaseDir, "packages");

            public string FirstRunCookie => _parent.GetOrComputePath(ref _firstRunCookie, BaseDir, ".firstrun");

            public string PackageCache => _parent.GetOrComputePath(ref _packageCache, _parent._environmentSettings.Paths.UserProfileDir, ".nuget", "packages");

            public string ScratchDir => _parent.GetOrComputePath(ref _scratchDir, BaseDir, "scratch");

            public string SettingsFile => _parent.GetOrComputePath(ref _settingsFile, BaseDir, "settings.json");

            public string GlobalSettingsFile => _parent.GetOrComputePath(ref _globalSettingsFile, _parent._environmentSettings.Paths.TemplateEngineRootDir, "settings.json");

            public string CultureNeutralTemplateCacheFile
            {
                get
                {
                    return ExplicitLocaleTemplateCacheFile(null);
                }
            }

            public string CurrentLocaleTemplateCacheFile
            {
                get
                {
                    return ExplicitLocaleTemplateCacheFile(CultureInfo.CurrentUICulture.Name);
                }
            }

            public readonly string TemplateCacheFileBaseName = "templatecache.json";
            private readonly Paths _parent;

            public string ExplicitLocaleTemplateCacheFile(string locale)
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

                // don't cache, the locale could change
                string tempCache = null;
                return _parent.GetOrComputePath(ref tempCache, BaseDir, filename);
            }

            public string NuGetConfig => _parent.GetOrComputePath(ref _nuGetConfig, BaseDir, "NuGet.config");
        }
    }
}
