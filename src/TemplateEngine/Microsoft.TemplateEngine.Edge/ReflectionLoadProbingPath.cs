using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.TemplateEngine.Edge
{
    public static class TypeEx
    {
        public static Type GetType(this string typeName)
        {
            int commaIndex = typeName.IndexOf(',');
            if (commaIndex < 0)
            {
                return Type.GetType(typeName);
            }

            string asmName = typeName.Substring(commaIndex + 1).Trim();

            if (!ReflectionLoadProbingPath.HasLoaded(asmName))
            {
                AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(asmName));
            }

            return Type.GetType(typeName);
        }
    }

    public class ReflectionLoadProbingPath
    {
        private static readonly ConcurrentDictionary<string, Assembly> LoadedAssemblies = new ConcurrentDictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        private static readonly List<ReflectionLoadProbingPath> Instance = new List<ReflectionLoadProbingPath>();

        private readonly string _path;

        private ReflectionLoadProbingPath(string path)
        {
            _path = path;
        }

        public static void Add(string basePath)
        {
            Instance.Add(new ReflectionLoadProbingPath(basePath));
            AssemblyLoadContext.Default.Resolving += Resolving;
        }

        public static bool HasLoaded(string assemblyName)
        {
            return LoadedAssemblies.ContainsKey(assemblyName);
        }

        public static void Reset()
        {
            Instance.Clear();
        }

        private static Assembly SelectBestMatch(AssemblyLoadContext loadContext, AssemblyName match, IEnumerable<FileInfo> candidates)
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

                    AssemblyName candidateName = AssemblyLoadContext.GetAssemblyName(file.FullName);

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
                        Assembly result = loadContext.LoadFromAssemblyPath(attempt);
                        return result;
                    }
                    catch
                    {
                    }
                }

                return null;
            });
        }

        private static Assembly Resolving(AssemblyLoadContext assemblyLoadContext, AssemblyName assemblyName)
        {
            foreach (ReflectionLoadProbingPath selector in Instance)
            {
                DirectoryInfo info = new DirectoryInfo(Path.Combine(selector._path, assemblyName.Name));
                Assembly found = null;

                if (info.Exists)
                {
                    IEnumerable<FileInfo> files = info.EnumerateFiles($"{assemblyName.Name}.dll", SearchOption.AllDirectories)
                        .Where(x => x.FullName.IndexOf($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) > -1
                        && (x.FullName.IndexOf($"{Path.DirectorySeparatorChar}netstandard1.", StringComparison.OrdinalIgnoreCase) > -1
                        || x.FullName.IndexOf($"{Path.DirectorySeparatorChar}netcoreapp1.", StringComparison.OrdinalIgnoreCase) > -1))
                        .OrderByDescending(x => x.FullName);
                    found = SelectBestMatch(assemblyLoadContext, assemblyName, files);

                    if (found != null)
                    {
                        foreach (AssemblyName reference in found.GetReferencedAssemblies())
                        {
                            Resolving(assemblyLoadContext, reference);
                        }
                    }
                }

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
