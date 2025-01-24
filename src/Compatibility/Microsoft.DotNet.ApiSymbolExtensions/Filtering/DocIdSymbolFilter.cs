// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiSymbolExtensions.Filtering
{
    /// <summary>
    /// Implements the logic of filtering out api.
    /// Reads the file with the list of attributes, types, members in DocId format.
    /// </summary>
    public class DocIdSymbolFilter : ISymbolFilter
    {
        private readonly HashSet<string> _docIdsToExclude;

        public static DocIdSymbolFilter CreateFromFiles(params string[] filesWithDocIdsToExclude)
            => new DocIdSymbolFilter(ReadDocIdsFromFiles(filesWithDocIdsToExclude));

        public static DocIdSymbolFilter CreateFromLists(params string[] docIdsToExclude)
            => new DocIdSymbolFilter(ReadDocIdsFromList(docIdsToExclude));

        private DocIdSymbolFilter(IEnumerable<string> docIdsToExclude)
            => _docIdsToExclude = [.. docIdsToExclude];

        /// <summary>
        ///  Determines whether the <see cref="ISymbol"/> should be included.
        /// </summary>
        /// <param name="symbol"><see cref="ISymbol"/> to evaluate.</param>
        /// <returns>True to include the <paramref name="symbol"/> or false to filter it out.</returns>
        public bool Include(ISymbol symbol)
        {
            string? docId = symbol.GetDocumentationCommentId();
            if (docId is not null && _docIdsToExclude.Contains(docId))
            {
                return false;
            }

            return true;
        }

        private static IEnumerable<string> ReadDocIdsFromList(params string[] ids)
        {
            foreach (string id in ids)
            {
#if NET
                if (!string.IsNullOrWhiteSpace(id) && !id.StartsWith('#') && !id.StartsWith("//"))
#else
                if (!string.IsNullOrWhiteSpace(id) && !id.StartsWith("#") && !id.StartsWith("//"))
#endif
                {
                    yield return id.Trim();
                }
            }
        }

        private static IEnumerable<string> ReadDocIdsFromFiles(params string[] docIdsToExcludeFiles)
        {
            foreach (string docIdsToExcludeFile in docIdsToExcludeFiles)
            {
                if (string.IsNullOrWhiteSpace(docIdsToExcludeFile))
                {
                    continue;
                }

                foreach (string docId in ReadDocIdsFromList(File.ReadAllLines(docIdsToExcludeFile)))
                {
                    yield return docId;
                }
            }
        }
    }
}
