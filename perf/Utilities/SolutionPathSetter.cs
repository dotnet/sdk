// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;

namespace Microsoft.CodeAnalysis.Tools.Perf
{
    public static class SolutionPathSetter
    {
        private static int _registered = 0;
        private static string _currentDirectory;
        public static string RepositoryRootDirectory => Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.Parent.Parent.Parent.Parent.Parent.Parent.FullName;

        public static void SetCurrentDirectory()
        {
            if (Interlocked.Increment(ref _registered) == 1)
            {
                _currentDirectory = Environment.CurrentDirectory;
                var solutionPath = RepositoryRootDirectory;
                Environment.CurrentDirectory = solutionPath;
            }
        }

        public static void UnsetCurrentDirectory()
        {
            if (Interlocked.Decrement(ref _registered) == 0)
            {
                Environment.CurrentDirectory = _currentDirectory;
                _currentDirectory = null;
            }
        }
    }
}
