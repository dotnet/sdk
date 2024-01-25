// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiSymbolExtensions.Filtering
{
    /// <summary>
    /// Implements the logic of filtering API.
    /// Reads the file with the list of attributes, types, members in DocId format.
    /// </summary>
    /// <param name="docIdsFiles">List of files which contain DocIds to operate on.</param>
    /// <param name="includeDocIds">Determines if DocIds should be included or excluded.  Defaults to false which excludes specified DocIds.</param>
    public class DocIdSymbolFilter(IEnumerable<string>? docIdsFiles = null, bool includeDocIds = false) : ISymbolFilter
    {
        private readonly HashSet<string> _docIds = new(ReadDocIdsAttributes(docIdsFiles));

        /// <summary>
        /// Determines if DocIds should be included or excluded.
        /// </summary>
        public bool IncludeDocIds { get; } = includeDocIds;

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
                return IncludeDocIds;
            }

            return !IncludeDocIds;
        }

        /// <summary>
        /// Add a single DocID to the filtered set.
        /// </summary>
        /// <param name="docId">DocID to add to the filtered set.</param>
        public void Add(string docId)
        {
            _docIds.Add(docId);
        }

        /// <summary>
        /// Add multiple docIDs to the filtered set.
        /// </summary>
        /// <param name="docIds">DocIDs to add to the filtered set.</param>
        public void AddRange(IEnumerable<string> docIds)
        {
            _docIds.UnionWith(docIds);
        }

        private static IEnumerable<string> ReadDocIdsAttributes(IEnumerable<string>? docIdsToExcludeFiles)
        {
            if (docIdsToExcludeFiles is null)
            {
                yield break;
            }

            foreach (string docIdsToExcludeFile in docIdsToExcludeFiles)
            {
                foreach (string id in File.ReadAllLines(docIdsToExcludeFile))
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
}
