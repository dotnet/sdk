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

        /// <summary>
        /// Creates a filter to exclude APIs using the DocIDs provided in the specified files.
        /// </summary>
        /// <param name="filesWithDocIdsToExclude">A collection of files each containing multiple DocIDs to exclude.</param>
        /// <returns>An instance of the symbol filter.</returns>
        public static DocIdSymbolFilter CreateFromFiles(params string[] filesWithDocIdsToExclude)
        {
            List<string> docIds = new();

            foreach (string docIdsToExcludeFile in filesWithDocIdsToExclude)
            {
                if (string.IsNullOrWhiteSpace(docIdsToExcludeFile))
                {
                    continue;
                }

                foreach (string docId in ReadDocIdsFromList(File.ReadAllLines(docIdsToExcludeFile)))
                {
                    docIds.Add(docId);
                }
            }

            return new DocIdSymbolFilter(docIds);
        }

        /// <summary>
        /// Creates a filter to exclude APIs using the DocIDs provided in the specified list.
        /// </summary>
        /// <param name="docIdsToExclude">A collection of DocIDs to exclude.</param>
        /// <returns>An instance of the symbol filter.</returns>
        public static DocIdSymbolFilter CreateFromLists(params string[] docIdsToExclude)
            => new DocIdSymbolFilter(ReadDocIdsFromList(docIdsToExclude));

        // Private constructor to avoid creating an instance with an empty list.
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

    }
}
