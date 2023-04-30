// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Collections.Generic;
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

        public DocIdSymbolFilter(string[] docIdsToExcludeFiles)
        {
            _docIdsToExclude = new HashSet<string>(ReadDocIdsAttributes(docIdsToExcludeFiles));
        }

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

        private static IEnumerable<string> ReadDocIdsAttributes(IEnumerable<string> excludeAttributesFiles)
        {
            foreach (string filePath in excludeAttributesFiles)
            {
                foreach (string id in File.ReadAllLines(filePath))
                {
                    if (!string.IsNullOrWhiteSpace(id) && !id.StartsWith("#") && !id.StartsWith("//"))
                    {
                        yield return id.Trim();
                    }
                }
            }
        }
    }
}
