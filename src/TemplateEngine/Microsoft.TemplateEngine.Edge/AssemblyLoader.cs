// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFULL
using System;
#endif

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.TemplateEngine.Edge.Settings;

#if !NETFULL

using System.Runtime.Loader;

#endif

namespace Microsoft.TemplateEngine.Edge
{
    internal static class AssemblyLoader
    {
        internal static IEnumerable<KeyValuePair<string, Assembly>> LoadAllFromPath(SettingsFilePaths paths, out IEnumerable<string> loadFailures, string path, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
#if !NETFULL
            return AssemblyLoadContext.Default.LoadAllFromPath(paths, out loadFailures, path, pattern, searchOption);
#else
            return AppDomain.CurrentDomain.LoadAllFromPath(paths, out loadFailures, path, pattern, searchOption);
#endif
        }
    }
}
