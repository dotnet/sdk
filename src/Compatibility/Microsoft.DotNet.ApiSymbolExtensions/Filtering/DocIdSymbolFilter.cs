// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiSymbolExtensions.Filtering
{
    /// <summary>
    /// Implements the logic of filtering api.
    /// Reads the file with the list of attributes, types, members in DocId format.
    /// </summary>
    public class DocIdSymbolFilter : ISymbolFilter
    {
        private readonly HashSet<string> _docIds;
        private readonly bool _includeDocIds;

        /// <summary>
        /// Creates a filter based on the DocIDs provided in the specified files.
        /// </summary>
        /// <param name="filesWithDocIds">A collection of files each containing multiple DocIDs.</param>
        /// <param name="includeDocIds">When <see langword="false"/> (the default), symbols matching the DocIDs are
        /// excluded. When <see langword="true"/>, only symbols matching the DocIDs are included.</param>
        /// <returns>An instance of the symbol filter.</returns>
        public static DocIdSymbolFilter CreateFromFiles(string[] filesWithDocIds, bool includeDocIds = false)
        {
            List<string> docIds = new();

            foreach (string docIdsFile in filesWithDocIds)
            {
                if (string.IsNullOrWhiteSpace(docIdsFile))
                {
                    continue;
                }

                foreach (string docId in ReadDocIdsFromList(File.ReadAllLines(docIdsFile)))
                {
                    docIds.Add(docId);
                }
            }

            return new DocIdSymbolFilter(docIds, includeDocIds);
        }

        /// <summary>
        /// Creates a filter based on the DocIDs provided in the specified list.
        /// </summary>
        /// <param name="docIds">A collection of DocIDs.</param>
        /// <param name="includeDocIds">When <see langword="false"/> (the default), symbols matching the DocIDs are
        /// excluded. When <see langword="true"/>, only symbols matching the DocIDs are included.</param>
        /// <returns>An instance of the symbol filter.</returns>
        public static DocIdSymbolFilter CreateFromLists(string[] docIds, bool includeDocIds = false)
            => new DocIdSymbolFilter(ReadDocIdsFromList(docIds), includeDocIds);

        // Private constructor to avoid creating an instance with an empty list.
        private DocIdSymbolFilter(IEnumerable<string> docIds, bool includeDocIds)
        {
            _docIds = [.. docIds];
            _includeDocIds = includeDocIds;
        }

        /// <summary>
        ///  Determines whether the <see cref="ISymbol"/> should be included.
        /// </summary>
        /// <param name="symbol"><see cref="ISymbol"/> to evaluate.</param>
        /// <returns>True to include the <paramref name="symbol"/> or false to filter it out.</returns>
        public bool Include(ISymbol symbol)
        {
            string? docId = symbol.GetDocumentationCommentId();
            if (docId is not null && _docIds.Contains(docId))
            {
                return _includeDocIds;
            }

            return !_includeDocIds;
        }

        private static IEnumerable<string> ReadDocIdsFromList(params string[] ids)
        {
            foreach (string id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id) && !id.StartsWith('#') && !id.StartsWith("//"))
                {
                    yield return id.Trim();
                }
            }
        }
    }
}
