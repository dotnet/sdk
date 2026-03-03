// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Text;

namespace Microsoft.CodeAnalysis.Tools.Workspaces
{
    internal static class CscCommandLineParser
    {
        /// <summary>
        /// Parses a raw csc command line string into individual arguments,
        /// handling quoted paths that contain spaces.
        /// </summary>
        public static string[] Parse(string commandLine)
        {
            var args = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            foreach (var c in commandLine)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        args.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                args.Add(current.ToString());
            }

            return args.ToArray();
        }
    }
}
