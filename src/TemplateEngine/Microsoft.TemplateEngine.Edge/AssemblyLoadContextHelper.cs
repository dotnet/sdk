using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
#if !NET451
using System.Runtime.Loader;
#endif

namespace Microsoft.TemplateEngine.Edge
{
    public static class AssemblyLoadContextHelper
    {
#if !NET451
        public static IEnumerable<Assembly> LoadAllFromCodebase(this AssemblyLoadContext context, out IEnumerable<string> loadFailures, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
#else
        public static IEnumerable<Assembly> LoadAllFromCodebase(this AppDomain context, out IEnumerable<string> loadFailures, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
#endif
        {
            return LoadAllFromPath(context, out loadFailures, Paths.Global.BaseDir, pattern, searchOption);
        }

#if !NET451
        public static IEnumerable<Assembly> LoadAllFromPath(this AssemblyLoadContext context, out IEnumerable<string> loadFailures, string path, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
#else
        public static IEnumerable<Assembly> LoadAllFromPath(this AppDomain context, out IEnumerable<string> loadFailures, string path, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
#endif
        {
            List<Assembly> loaded = new List<Assembly>();
            List<string> failures = new List<string>();

            foreach (string file in path.EnumerateFiles(pattern, searchOption))
            {
                try
                {
                    Assembly assembly = null;

#if !NET451
                    if(file.IndexOf("netcoreapp", StringComparison.OrdinalIgnoreCase) > -1 || file.IndexOf("netstandard", StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        assembly = context.LoadFromAssemblyPath(file);
                    }
#else
                    if (file.IndexOf("net4", StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        assembly = Assembly.LoadFile(file);
                    }
#endif

                    if (assembly != null)
                    {
                        loaded.Add(assembly);
                    }
                }
                catch
                {
                    failures.Add(file);
                }
            }

            loadFailures = failures;
            return loaded;
        }
    }
}
