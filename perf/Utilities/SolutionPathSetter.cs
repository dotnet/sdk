// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;

namespace Microsoft.CodeAnalysis.Tools.Perf
{
    public static class SolutionPathSetter
    {
        private static int s_registered;
        private static string s_currentDirectory;
        public static string RepositoryRootDirectory => Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.Parent.Parent.Parent.Parent.Parent.Parent.FullName;

        public static void SetCurrentDirectory()
        {
            if (Interlocked.Increment(ref s_registered) == 1)
            {
                s_currentDirectory = Environment.CurrentDirectory;
                var info = new DirectoryInfo(s_currentDirectory);
                while (true)
                {
                    if (File.Exists(Path.Combine(info.FullName, "format.sln")))
                    {
                        break;
                    }

                    info = info.Parent;
                }

                Environment.CurrentDirectory = info.FullName;
            }
        }

        public static void UnsetCurrentDirectory()
        {
            if (Interlocked.Decrement(ref s_registered) == 0)
            {
                Environment.CurrentDirectory = s_currentDirectory;
                s_currentDirectory = null;
            }
        }
    }
}
