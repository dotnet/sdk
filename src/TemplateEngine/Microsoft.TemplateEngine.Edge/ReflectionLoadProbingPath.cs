// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Reflection;

#if !NETFRAMEWORK

using System.Runtime.Loader;

#endif

namespace Microsoft.TemplateEngine.Edge
{
    internal class ReflectionLoadProbingPath
    {
        private static readonly ConcurrentDictionary<string, Assembly?> LoadedAssemblies = new ConcurrentDictionary<string, Assembly?>(StringComparer.OrdinalIgnoreCase);

        private static readonly List<ReflectionLoadProbingPath> Instance = new List<ReflectionLoadProbingPath>();

        private readonly string _path;

        private ReflectionLoadProbingPath(string path)
        {
            _path = path;
        }

        internal static void Add(string basePath)
        {
            Instance.Add(new ReflectionLoadProbingPath(basePath));
#if NETFRAMEWORK
            AppDomain.CurrentDomain.AssemblyResolve += Resolving;
#else
            AssemblyLoadContext.Default.Resolving += Resolving;
#endif
        }

        internal static bool HasLoaded(string assemblyName)
        {
            return LoadedAssemblies.ContainsKey(assemblyName);
        }

        internal static void Reset()
        {
            Instance.Clear();
        }

#if NETFRAMEWORK
        private static Assembly? SelectBestMatch(object sender, AssemblyName match, IEnumerable<FileInfo> candidates)
#else
        private static Assembly? SelectBestMatch(AssemblyLoadContext loadContext, AssemblyName match, IEnumerable<FileInfo> candidates)
#endif
        {
            return LoadedAssemblies.GetOrAdd(match.ToString(), n =>
            {
                Stack<string> bestMatch = new Stack<string>();
                byte[] pk = match.GetPublicKey();
                bool cultureMatch = false;
                bool majorVersionMatch = false;
                bool minorVersionMatch = false;
                bool buildMatch = false;
                bool revisionMatch = false;

                foreach (FileInfo file in candidates)
                {
                    if (!file.Exists)
                    {
                        continue;
                    }

#if NETFRAMEWORK
                    AssemblyName candidateName = AssemblyName.GetAssemblyName(file.FullName);
#else
                    AssemblyName candidateName = AssemblyLoadContext.GetAssemblyName(file.FullName);
#endif

                    //Only pursue things that may have the same identity
                    if (!string.Equals(candidateName.Name, match.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    //If the required match has a strong name, the public key token must match
                    if (pk != null && !pk.SequenceEqual(candidateName.GetPublicKey() ?? Enumerable.Empty<byte>()))
                    {
                        continue;
                    }

                    if (match.Version != null)
                    {
                        //Don't go backwards
                        if (candidateName.Version.Major < match.Version.Major)
                        {
                            continue;
                        }

                        if (candidateName.Version.Major == match.Version.Major)
                        {
                            //Don't go backwards
                            if (candidateName.Version.Minor < match.Version.Minor)
                            {
                                continue;
                            }

                            if (candidateName.Version.Minor == match.Version.Minor)
                            {
                                //Don't go backwards
                                if (candidateName.Version.Build < match.Version.Build)
                                {
                                    continue;
                                }

                                if (candidateName.Version.Build == match.Version.Build)
                                {
                                    //Don't go backwards
                                    if (candidateName.Version.Revision < match.Version.Revision)
                                    {
                                        continue;
                                    }

                                    if (candidateName.Version.Revision != match.Version.Revision)
                                    {
                                        if (revisionMatch)
                                        {
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        revisionMatch = true;
                                    }

                                    majorVersionMatch = true;
                                    minorVersionMatch = true;
                                    buildMatch = true;
                                }
                                else
                                {
                                    if (buildMatch)
                                    {
                                        continue;
                                    }

                                    majorVersionMatch = true;
                                    minorVersionMatch = true;
                                }
                            }
                            else
                            {
                                if (minorVersionMatch)
                                {
                                    continue;
                                }

                                majorVersionMatch = true;
                            }
                        }
                        else
                        {
                            if (majorVersionMatch)
                            {
                                continue;
                            }
                        }
                    }

                    if (string.Equals(candidateName.CultureName, match.CultureName, StringComparison.OrdinalIgnoreCase))
                    {
                        cultureMatch = true;
                    }
                    else if (cultureMatch)
                    {
                        continue;
                    }

                    bestMatch.Push(file.FullName);
                }

                while (bestMatch.Count > 0)
                {
                    try
                    {
                        string attempt = bestMatch.Pop();
#if NETFRAMEWORK
                        Assembly result = Assembly.LoadFile(attempt);
#else
                        Assembly result = loadContext.LoadFromAssemblyPath(attempt);
#endif
                        return result;
                    }
                    catch
                    {
                    }
                }

                return null;
            });
        }

#if NETFRAMEWORK
        private static Assembly? Resolving(object sender, ResolveEventArgs resolveEventArgs)
#else
        private static Assembly? Resolving(AssemblyLoadContext assemblyLoadContext, AssemblyName assemblyName)
#endif
        {
#if NETFRAMEWORK
            string stringName = resolveEventArgs.Name;
            AssemblyName assemblyName = new AssemblyName(stringName);
#else
            string stringName = assemblyName.Name;
#endif

            foreach (ReflectionLoadProbingPath selector in Instance)
            {
                DirectoryInfo info = new DirectoryInfo(Path.Combine(selector._path, stringName));
                Assembly? found = null;

                if (info.Exists)
                {
                    IEnumerable<FileInfo> files = info.EnumerateFiles($"{stringName}.dll", SearchOption.AllDirectories)
                        .Where(x => x.FullName.IndexOf($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) > -1
                        && (x.FullName.IndexOf($"{Path.DirectorySeparatorChar}netstandard", StringComparison.OrdinalIgnoreCase) > -1
                        || x.FullName.IndexOf($"{Path.DirectorySeparatorChar}netcoreapp", StringComparison.OrdinalIgnoreCase) > -1))
                        .OrderByDescending(x => x.FullName);
#if NETFRAMEWORK
                    found = SelectBestMatch(sender, assemblyName, files);
#else
                    found = SelectBestMatch(assemblyLoadContext, assemblyName, files);
#endif
                }
                else if (File.Exists(Path.Combine(selector._path, stringName + ".dll")))
                {
                    FileInfo f = new FileInfo(Path.Combine(selector._path, stringName + ".dll"));
                    FileInfo[] files = { f };
#if NETFRAMEWORK
                    found = SelectBestMatch(sender, assemblyName, files);
#else
                    found = SelectBestMatch(assemblyLoadContext, assemblyName, files);
#endif
                }

                if (found != null)
                {
                    foreach (AssemblyName reference in found.GetReferencedAssemblies())
                    {
#if NETFRAMEWORK
                        ResolveEventArgs referenceArgs = new ResolveEventArgs(reference.FullName, found);
                        Resolving(sender, referenceArgs);
#else
                        Resolving(assemblyLoadContext, reference);
#endif
                    }

                    return found;
                }
            }

            return null;
        }
    }
}
