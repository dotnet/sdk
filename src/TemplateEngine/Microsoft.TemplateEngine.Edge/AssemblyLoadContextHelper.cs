using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.TemplateEngine.Edge
{
    public static class AssemblyLoadContextHelper
    {
        public static IEnumerable<Assembly> LoadAllFromCodebase(this AssemblyLoadContext context, out IEnumerable<string> loadFailures, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
            return LoadAllFromPath(context, out loadFailures, Paths.Global.BaseDir, pattern, searchOption);
        }

        public static IEnumerable<Assembly> LoadAllFromPath(this AssemblyLoadContext context, out IEnumerable<string> loadFailures, string path, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
            List<Assembly> loaded = new List<Assembly>();
            List<string> failures = new List<string>();

            foreach (string file in path.EnumerateFiles(pattern, searchOption))
            {
                try
                {
                    Assembly assembly = context.LoadFromAssemblyPath(file);
                    loaded.Add(assembly);
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
