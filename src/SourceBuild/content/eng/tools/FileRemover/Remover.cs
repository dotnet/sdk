// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.FileSystemGlobbing;

namespace FileRemover

{
    public static class Remover
    {
        public static void RemoveFiles(string targetDirectory, string removalFile)
        {
            var filesToRemove = new List<string>();

            if (!string.IsNullOrEmpty(removalFile))
            {
                var filePatterns = File.ReadAllLines(removalFile).ToList();
                filesToRemove.AddRange(ParseFileGlobPatterns(filePatterns, targetDirectory));
            }
            else
            {
                throw new ArgumentException("No removal file specified.");
            }

            foreach (var file in filesToRemove.Where(File.Exists))
            {
                File.Delete(file);
            }
        }

        private static IEnumerable<string> ParseFileGlobPatterns(List<string> filePatterns, string targetDirectory)
        {
            var matcher = new Matcher(StringComparison.Ordinal);
            foreach (var pattern in filePatterns)
            {
                matcher.AddInclude(pattern);
            }

            return matcher.GetResultsInFullPath(targetDirectory);
        }
    }
}
