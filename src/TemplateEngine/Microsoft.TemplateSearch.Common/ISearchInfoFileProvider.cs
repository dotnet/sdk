// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;

namespace Microsoft.TemplateSearch.Common
{
    internal interface ISearchInfoFileProvider
    {
        /// <summary>
        /// Sets up the search metadata file.
        /// The provider can get it however is appropriate. The file must be placed in the input metadataFileTargetLocation.
        /// </summary>
        /// <param name="paths">A Paths instance, so the abstracted file system operations are available.</param>
        /// <param name="metadataFileTargetLocation">The expected location of the metadata file, after this is run.</param>
        /// <returns></returns>
        Task<bool> TryEnsureSearchFileAsync(IEngineEnvironmentSettings environmentSettings, Paths paths, string metadataFileTargetLocation);
    }
}
