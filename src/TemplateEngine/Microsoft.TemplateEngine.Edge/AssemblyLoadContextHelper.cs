// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.TemplateEngine.Edge.Settings;

#if !NETFULL

using System.Runtime.Loader;

#endif

namespace Microsoft.TemplateEngine.Edge
{
    internal static class AssemblyLoadContextHelper
    {
#if !NETFULL

        internal static IEnumerable<KeyValuePair<string, Assembly>> LoadAllFromPath(this AssemblyLoadContext context, SettingsFilePaths paths, out IEnumerable<string> loadFailures, string path, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
#else
        internal static IEnumerable<KeyValuePair<string, Assembly>> LoadAllFromPath(this AppDomain context, SettingsFilePaths paths, out IEnumerable<string> loadFailures, string path, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
#endif
        {
            List<KeyValuePair<string, Assembly>> loaded = new List<KeyValuePair<string, Assembly>>();
            List<string> failures = new List<string>();

            foreach (string file in paths.EnumerateFiles(path, pattern, searchOption))
            {
                try
                {
                    Assembly assembly = null;

#if !NETFULL
                    if (file.IndexOf("netcoreapp", StringComparison.OrdinalIgnoreCase) > -1 || file.IndexOf("netstandard", StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        using (Stream fileStream = paths.OpenRead(file))
                        {
                            assembly = context.LoadFromStream(fileStream);
                        }
                    }
#else
                    if (file.IndexOf("net4", StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        byte[] fileBytes = paths.ReadAllBytes(file);
                        assembly = Assembly.Load(fileBytes);
                    }
#endif

                    if (assembly != null)
                    {
                        loaded.Add(new KeyValuePair<string, Assembly>(file, assembly));
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
