#if NET45
using System;
#endif
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
#if !NET45
using System.Runtime.Loader;
#endif

namespace Microsoft.TemplateEngine.Edge
{
    public static class AssemblyLoader
    {
        public static Assembly Load(string path)
        {
#if !NET45
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
#else
            return Assembly.LoadFile(path);
#endif
        }

        public static IEnumerable<Assembly> LoadAllAssemblies(out IEnumerable<string> loadFailures, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
            IEnumerable<Assembly> loaded = LoadAllFromUserDir(out IEnumerable<string> failures1, pattern, searchOption).Union(LoadAllFromCodebase(out IEnumerable<string> failures2, pattern, searchOption));
            loadFailures = failures1.Union(failures2);
            return loaded;
        }

        public static IEnumerable<Assembly> LoadAllFromCodebase(out IEnumerable<string> loadFailures, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
#if !NET45
            return AssemblyLoadContext.Default.LoadAllFromCodebase(out loadFailures, pattern, searchOption);
#else
            return AppDomain.CurrentDomain.LoadAllFromCodebase(out loadFailures, pattern, searchOption);
#endif
        }

        public static IEnumerable<Assembly> LoadAllFromUserDir(out IEnumerable<string> loadFailures, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
            return LoadAllFromPath(out loadFailures, Paths.User.Content, pattern, searchOption);
        }

        public static IEnumerable<Assembly> LoadAllFromPath(out IEnumerable<string> loadFailures, string path, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
#if !NET45
            return AssemblyLoadContext.Default.LoadAllFromPath(out loadFailures, path, pattern, searchOption);
#else
            return AppDomain.CurrentDomain.LoadAllFromPath(out loadFailures, path, pattern, searchOption);
#endif
        }
    }
}
