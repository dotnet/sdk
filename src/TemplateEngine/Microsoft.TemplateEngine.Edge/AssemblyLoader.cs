#if NETFULL
using System;
#endif
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
#if !NETFULL
using System.Runtime.Loader;
#endif

namespace Microsoft.TemplateEngine.Edge
{
    public static class AssemblyLoader
    {
        public static Assembly Load(string path)
        {
#if !NETFULL
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
#else
            return Assembly.LoadFile(path);
#endif
        }

        public static IEnumerable<KeyValuePair<string, Assembly>> LoadAllAssemblies(Paths paths, out IEnumerable<string> loadFailures, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
            IEnumerable<KeyValuePair<string, Assembly>> loaded = LoadAllFromUserDir(paths, out IEnumerable<string> failures1, pattern, searchOption).Union(LoadAllFromCodebase(paths, out IEnumerable<string> failures2, pattern, searchOption));
            loadFailures = failures1.Union(failures2);
            return loaded;
        }

        public static IEnumerable<KeyValuePair<string, Assembly>> LoadAllFromCodebase(Paths paths, out IEnumerable<string> loadFailures, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
#if !NETFULL
            return AssemblyLoadContext.Default.LoadAllFromCodebase(paths, out loadFailures, pattern, searchOption);
#else
            return AppDomain.CurrentDomain.LoadAllFromCodebase(paths, out loadFailures, pattern, searchOption);
#endif
        }

        public static IEnumerable<KeyValuePair<string, Assembly>> LoadAllFromUserDir(Paths paths, out IEnumerable<string> loadFailures, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
            return LoadAllFromPath(paths, out loadFailures, paths.User.Content, pattern, searchOption);
        }

        public static IEnumerable<KeyValuePair<string, Assembly>> LoadAllFromPath(Paths paths, out IEnumerable<string> loadFailures, string path, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
#if !NETFULL
            return AssemblyLoadContext.Default.LoadAllFromPath(paths, out loadFailures, path, pattern, searchOption);
#else
            return AppDomain.CurrentDomain.LoadAllFromPath(paths, out loadFailures, path, pattern, searchOption);
#endif
        }
    }
}
